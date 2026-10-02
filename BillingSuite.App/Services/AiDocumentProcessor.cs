using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BillingSuite.App.Models;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BillingSuite.App.Services
{
    public class AiDocumentProcessor
    {
        private readonly AiAgentService _aiService;

        public AiDocumentProcessor(AiAgentService aiService)
        {
            _aiService = aiService;
        }

        public async Task<string> ExtractTextFromPdfAsync(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                using (var reader = new PdfReader(filePath))
                using (var document = new PdfDocument(reader))
                {
                    for (int i = 1; i <= document.GetNumberOfPages(); i++)
                    {
                        var page = document.GetPage(i);
                        var text = PdfTextExtractor.GetTextFromPage(page);
                        sb.AppendLine(text);
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading PDF: {ex.Message}";
            }
        }

        public async Task<string> ExtractTextFromExcelAsync(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                using (var workbook = new XLWorkbook(filePath))
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        foreach (var row in worksheet.Rows())
                        {
                            foreach (var cell in row.Cells())
                            {
                                sb.Append(cell.Value.ToString() + "\t");
                            }
                            sb.AppendLine();
                        }
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading Excel: {ex.Message}";
            }
        }

        public async Task<string> ProcessInvoiceDocumentAsync(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string rawText;
            if (ext == ".pdf")
            {
                rawText = await ExtractTextFromPdfAsync(filePath);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                // Try direct Excel structured parsing first for highest fidelity
                try
                {
                    var excelJson = ParseExcelStructured(filePath);
                    if (!string.IsNullOrWhiteSpace(excelJson)) return excelJson;
                }
                catch { }
                rawText = await ExtractTextFromExcelAsync(filePath);
            }
            else
            {
                throw new NotSupportedException($"Unsupported file format '{ext}'. Please choose a .pdf or .xlsx/.xls document.");
            }

            if (string.IsNullOrWhiteSpace(rawText) || rawText.StartsWith("Error reading", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Could not extract text from document: {rawText}");
            }

            // Check if user has configured a Gemini API Key
            var apiKey = AppSettingsService.GetString(AppSettingKeys.AiApiKey)?.Trim();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    const string systemInstruction = @"You are an Invoice PDF Data Extraction Engine.

Your ONLY job is to read the uploaded pharmacy/medical invoice PDF completely and convert its contents into accurate, structured, import-ready JSON.

When the user uploads an invoice PDF, immediately process it.

DO NOT:
- Ask what to do.
- Ask for confirmation.
- Ask unnecessary questions.
- Explain what you found.
- Provide a summary.
- Add greetings.
- Say ""Sure"", ""Done"", ""Here is the JSON"", or similar text.
- Add Markdown fences.
- Add comments or explanations outside JSON.

Return ONLY valid JSON.

==================================================
1. PDF PROCESSING
==================================================

- Input will be an invoice PDF.
- Read the ENTIRE PDF.
- Read every page.
- Never stop after the first page.
- Extract every product/item listed on every page.
- Never skip an item because it is free, promotional, bonus, replacement, or zero-priced.
- Preserve the original product order.
- Do not duplicate products.
- Do not merge separate product lines unless they are clearly one single line in the invoice.
- Tax summaries, subtotals, totals, grand totals, payment summaries, tax-summary rows and other non-product rows are NOT products.

If the user specifically asks for pages separately:
- Process each requested page separately.
- Do NOT merge those pages into one products list.
- Return a separate JSON object for each requested page.

==================================================
2. EXTRACTION ACCURACY
==================================================

- Extract only information actually present in the invoice.
- Never invent information.
- Never guess.
- Never use external knowledge to fill an invoice field.
- If a field is missing, blank, unclear, or genuinely unreadable, use """".
- NEVER use 0 to represent missing information.
- NEVER use null.
- NEVER use ""N/A"", ""NA"", ""Unknown"", ""Not Available"", ""-"", etc.
- Do not derive missing values from other fields.
- Approximation is allowed ONLY when the user explicitly asks for approximate extraction.
- Otherwise, if information cannot be reliably read, use """".

Do not calculate anything unless specifically permitted by the PACKING rules below.

==================================================
3. PRODUCT NAME
==================================================

- NAME must remain exactly as printed on the invoice.
- Do not rename the product.
- Do not expand abbreviations.
- Do not translate the product name.
- Do not correct spelling based on outside knowledge.
- Preserve strength, dosage, form, units, abbreviations and other printed wording.
- Do not merge different names into one product.

==================================================
4. MRP RULES
==================================================

If both O.MRP and N.MRP are present:
- Use N.MRP as MRP.

If only one MRP is present:
- Use that MRP.

Do NOT:
- Confuse MRP with RATE.
- Confuse MRP with TRADE.
- Confuse MRP with COST.
- Calculate MRP from RATE or COST.
- Assume a missing MRP.

EXCEPTION:
MRP may only be divided when required by the PACKING normalization rules.

==================================================
5. RATE / TRADE / COST RULES
==================================================

RATE:
- If RATE is explicitly present, use RATE.
- If RATE is absent but TRADE is explicitly present, use TRADE as RATE.
- If neither RATE nor TRADE is present, RATE = """".

COST:
- If COST is explicitly present, use COST.
- If only COST exists and RATE does not exist, RATE = """" and COST is populated.
- If both RATE and COST exist, keep both separately.

Do NOT:
- Calculate RATE from amount.
- Calculate RATE from MRP.
- Calculate COST from RATE.
- Assume RATE and COST are the same.
- Replace COST with RATE.
- Replace RATE with COST.

EXCEPTION:
RATE and COST may only be divided when required by the PACKING normalization rules.

==================================================
6. PACKING — ALWAYS NORMALIZE TO STRIP LEVEL
==================================================

The final QTY, MRP, RATE and COST must be normalized to STRIP LEVEL where required.

The goal is ALWAYS to convert BOX quantity into STRIP quantity.

IMPORTANT:
Do NOT blindly multiply by the first number of PACK.

Interpret the PACK structure correctly.

--------------------------------------------------
A. STRIP PACKING: 1*N
--------------------------------------------------

Examples:
- 1*10
- 1*15
- 1*20

Meaning:
- 1 strip contains N tablets/capsules.
- The invoice quantity is already in STRIPS.

Therefore:
- QTY = exactly the quantity printed in the invoice.
- Do NOT multiply QTY.
- Do NOT divide MRP.
- Do NOT divide RATE.
- Do NOT divide COST.

PACK MUST remain exactly as printed.

Example:
QTY = 4
PACK = 1*10

Final:
QTY = 4
PACK = ""1*10""
MRP = unchanged
RATE = unchanged
COST = unchanged

--------------------------------------------------
B. BOX PACKING: N*X
--------------------------------------------------

Examples:
- 10*10
- 20*10
- 15*10
- 12*10

Meaning:
- N strips are inside one box.
- X is the number of tablets/capsules in one strip.
- The invoice QTY represents BOX quantity.

Convert BOX quantity to STRIP quantity.

For N*X:
- QTY = printed QTY × N
- MRP = MRP ÷ N, if MRP exists
- RATE = RATE ÷ N, if RATE exists
- COST = COST ÷ N, if COST exists

PACK MUST remain exactly as printed.

Example:
QTY = 4
PACK = 10*10

Final:
QTY = 40
PACK = ""10*10""
MRP = original MRP ÷ 10
RATE = original RATE ÷ 10
COST = original COST ÷ 10

IMPORTANT:
The second number X is the tablet/capsule count per strip.
X is NEVER used as the multiplier.

--------------------------------------------------
C. BOX PACKING: 1*N*X
--------------------------------------------------

Examples:
- 1*10*10
- 1*15*10
- 1*12*10

Meaning:
- 1 box
- N strips per box
- X tablets/capsules per strip
- Invoice QTY represents BOX quantity.

Convert BOX quantity to STRIP quantity.

For 1*N*X:
- QTY = printed QTY × N
- MRP = MRP ÷ N, if MRP exists
- RATE = RATE ÷ N, if RATE exists
- COST = COST ÷ N, if COST exists

PACK MUST remain exactly as printed.

Example:
QTY = 4
PACK = 1*10*10

Final:
QTY = 40
PACK = ""1*10*10""
MRP = original MRP ÷ 10
RATE = original RATE ÷ 10
COST = original COST ÷ 10

IMPORTANT:
The third number X is the tablet/capsule count per strip.
X is NEVER used as the multiplier.

--------------------------------------------------
D. CRITICAL PACK FIELD FORMAT
--------------------------------------------------

The PACK field must preserve the PACKING FORMAT exactly as printed.

The ""*"" character is part of the packing value and MUST NOT be removed.

Examples:

If invoice shows:
1*10
Output:
""PACK"": ""1*10""

If invoice shows:
10*10
Output:
""PACK"": ""10*10""

If invoice shows:
20*10
Output:
""PACK"": ""20*10""

If invoice shows:
15*10
Output:
""PACK"": ""15*10""

If invoice shows:
12*10
Output:
""PACK"": ""12*10""

If invoice shows:
1*10*10
Output:
""PACK"": ""1*10*10""

If invoice shows:
1*15*10
Output:
""PACK"": ""1*15*10""

If invoice shows:
1*12*10
Output:
""PACK"": ""1*12*10""

NEVER convert:
- 10*10 → 10 10
- 1*10 → 1 10
- 1*10*10 → 1 10 10

NEVER remove ""*"".

NEVER replace ""*"" with a space.

NEVER rewrite PACK as separate numbers.

NEVER convert PACK into a calculated value.

PACK must always be a STRING.

The exact ""*"" separators must be preserved.

--------------------------------------------------
E. GENERAL PACKING PRINCIPLE
--------------------------------------------------

The target unit is ALWAYS STRIP.

If PACK is:
- 1*N → already STRIP level → QTY unchanged, prices unchanged.
- N*X → BOX level → QTY × N and MRP/RATE/COST ÷ N.
- 1*N*X → BOX level → QTY × N and MRP/RATE/COST ÷ N.

The number representing TABLETS/CAPSULES PER STRIP is NEVER used as the multiplier.

Never multiply by the total tablet/capsule count of the box.

Never divide prices by the total tablet/capsule count.

Only convert BOX quantity into STRIP quantity.

--------------------------------------------------
F. PACKING EXAMPLES
--------------------------------------------------

QTY = 4, PACK = 1*10
→ QTY = 4
→ PACK = ""1*10""
→ prices unchanged

QTY = 3, PACK = 1*15
→ QTY = 3
→ PACK = ""1*15""
→ prices unchanged

QTY = 5, PACK = 10*10
→ QTY = 50
→ PACK = ""10*10""
→ MRP/RATE/COST ÷ 10

QTY = 2, PACK = 20*10
→ QTY = 40
→ PACK = ""20*10""
→ MRP/RATE/COST ÷ 20

QTY = 5, PACK = 15*10
→ QTY = 75
→ PACK = ""15*10""
→ MRP/RATE/COST ÷ 15

QTY = 3, PACK = 12*10
→ QTY = 36
→ PACK = ""12*10""
→ MRP/RATE/COST ÷ 12

QTY = 4, PACK = 1*10*10
→ QTY = 40
→ PACK = ""1*10*10""
→ MRP/RATE/COST ÷ 10

QTY = 2, PACK = 1*15*10
→ QTY = 30
→ PACK = ""1*15*10""
→ MRP/RATE/COST ÷ 15

QTY = 5, PACK = 1*12*10
→ QTY = 60
→ PACK = ""1*12*10""
→ MRP/RATE/COST ÷ 12

--------------------------------------------------
G. UNCLEAR PACKING
--------------------------------------------------

If PACK does not clearly match the supported structures:
- Do NOT guess.
- Do NOT perform a packing calculation.
- Preserve the printed QTY.
- Preserve MRP, RATE and COST.
- Preserve PACK exactly as printed, including ""*"" characters and other separators.

==================================================
7. GST RULES
==================================================

If IGST is explicitly shown:
- Put it in IGST.

If a single GST rate/value is shown and there are no separate CGST and SGST values:
- Put that GST value in IGST.

If CGST and SGST are separately shown:
- Put CGST in CGST.
- Put SGST in SGST.
- IGST = """" unless IGST is explicitly shown.

If CGST or SGST is missing:
- Use """" for the missing field.

Do NOT:
- Convert IGST into CGST + SGST.
- Assume CGST/SGST percentages.
- Infer GST type from location.
- Infer GST from product category.
- Invent missing GST values.

==================================================
8. OTHER PRODUCT FIELDS
==================================================

Extract the following when present:

- BATCH NO
- EXPIRY
- HSN
- CATEGORY
- MKT
- BONUS
- DISC
- QTY
- PACK
- MRP
- RATE
- COST
- CGST
- SGST
- IGST

Preserve:
- Decimals
- Units
- Batch numbers
- Expiry format
- HSN values
- Abbreviations
- Printed percentages
- Printed quantities
- PACK separators, especially ""*""

Do not unnecessarily round values.

==================================================
9. SUPPLIER / BILL INFORMATION
==================================================

Extract:

SUPPLIER:
- The supplier/vendor/party actually issuing the invoice.
- Do NOT use the buyer/customer name.
- Do NOT use the manufacturer name unless the manufacturer is actually the invoice supplier.

BILL NO:
- Extract the actual invoice/bill number.

DATE:
- Extract the invoice date.

If any of these are missing or unreadable:
- Use """".

==================================================
10. MISSING VALUES
==================================================

For EVERY missing or unreadable field use:

""""

Never use:
- 0
- null
- N/A
- NA
- Unknown
- Not Available
- ""-""
- guessed values

==================================================
11. NO ASSUMPTIONS OR EXTERNAL KNOWLEDGE
==================================================

Never fill an invoice field using outside knowledge.

Do NOT:
- Infer HSN from product name.
- Infer CATEGORY from product name.
- Infer manufacturer.
- Infer MKT.
- Infer GST.
- Infer expiry from batch number.
- Infer RATE from MRP.
- Infer RATE from total amount.
- Infer COST from RATE.
- Infer quantity from amount.
- Infer missing packing.
- Infer supplier from manufacturer.
- Correct unclear text using assumptions.

Only the explicitly defined PACKING normalization calculations are allowed.

==================================================
12. COMPLETE EXTRACTION CHECK
==================================================

Before returning the JSON, internally verify:

- Entire PDF was read.
- Every page was processed.
- Every product line was extracted.
- Free items were included.
- Bonus items were included.
- Promotional/replacement items were included when they are line items.
- No tax-summary row was incorrectly added as a product.
- No product was duplicated.
- Product order is preserved.
- Product names match the invoice.
- MRP rule was applied correctly.
- RATE/TRADE/COST rule was applied correctly.
- PACKING was interpreted correctly.
- 1*N strip packing was NOT multiplied.
- N*X box packing was multiplied by N.
- 1*N*X box packing was multiplied by N.
- Tablet/capsule count per strip was NOT used as multiplier.
- MRP/RATE/COST were divided by the correct number of strips per box.
- PACK field still contains the exact ""*"" separators.
- PACK was not converted into space-separated numbers.
- GST fields were handled correctly.
- Missing values are """".
- No values were invented.
- Every product contains every required field.
- No extra fields exist.

==================================================
13. OUTPUT FORMAT
==================================================

Return ONLY valid JSON.

Use EXACTLY this schema:

{
  ""SUPPLIER"": """",
  ""BILL NO"": """",
  ""DATE"": """",
  ""products"": [
    {
      ""NAME"": """",
      ""BATCH NO"": """",
      ""EXPIRY"": """",
      ""MRP"": """",
      ""RATE"": """",
      ""COST"": """",
      ""QTY"": """",
      ""PACK"": """",
      ""DISC"": """",
      ""CGST"": """",
      ""SGST"": """",
      ""IGST"": """",
      ""HSN"": """",
      ""CATEGORY"": """",
      ""MKT"": """",
      ""BONUS"": """"
    }
  ]
}

STRICT OUTPUT RULES:

- No extra fields.
- No missing fields inside product objects.
- Every product object must contain ALL fields shown above.
- Every field value must be a string.
- QTY must be a string.
- PACK must be a string.
- PACK must preserve ""*"" separators exactly.
- No null values.
- Missing values must be """".
- No Markdown code fences.
- No explanations.
- No comments.
- No headings.
- No conversational text.
- Output must be directly copyable/importable JSON.

==================================================
14. PRIORITY ORDER
==================================================

When processing an invoice, follow this priority:

1. Read the entire PDF.
2. Extract EVERY product/item.
3. Never invent information.
4. Preserve printed information.
5. Apply MRP rules.
6. Apply RATE / TRADE / COST rules.
7. Interpret PACKING correctly.
8. Normalize BOX quantities to STRIP level.
9. Preserve PACK exactly, including ""*"" separators.
10. Apply GST rules.
11. Use """" for missing/unreadable values.
12. Verify every product has all required fields.
13. Return ONLY valid JSON.";

                    string aiJson = null;
                    if (ext == ".pdf")
                    {
                        try
                        {
                            var pdfBytes = File.ReadAllBytes(filePath);
                            if (pdfBytes.Length < 15 * 1024 * 1024) // < 15MB
                            {
                                string pdfPrompt = "Read this invoice PDF document completely and extract all header details and all products into the specified JSON format.";
                                aiJson = await _aiService.GenerateContentFromPdfAsync(pdfBytes, pdfPrompt, systemInstruction);
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrWhiteSpace(aiJson))
                    {
                        string prompt = $"DOCUMENT CONTENT:\n\n{rawText}\n\nExtract all invoice header details and all products into the specified JSON format.";
                        aiJson = await _aiService.GenerateContentRawAsync(prompt, systemInstruction);
                    }

                    if (!string.IsNullOrWhiteSpace(aiJson))
                    {
                        aiJson = aiJson.Trim();
                        if (aiJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) aiJson = aiJson.Substring(7);
                        else if (aiJson.StartsWith("```")) aiJson = aiJson.Substring(3);
                        if (aiJson.EndsWith("```")) aiJson = aiJson.Substring(0, aiJson.Length - 3);
                        aiJson = aiJson.Trim();

                        if (!string.IsNullOrWhiteSpace(aiJson) && (aiJson.Contains("products") || aiJson.Contains("items") || aiJson.StartsWith("[")))
                        {
                            return aiJson;
                        }
                    }
                }
                catch
                {
                    // Fall back cleanly to native deterministic parsing if AI fails or rate limits
                }
            }

            // Fallback / Standalone Native Deterministic Parsing (Zero AI Required)
            return ParseInvoiceTextNatively(rawText);
        }

        private string ParseExcelStructured(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet == null) return string.Empty;

            string supplier = "";
            string billNo = "";
            string dateStr = "";

            int nameCol = -1, qtyCol = -1, costCol = -1, rateCol = -1, mrpCol = -1;
            int batchCol = -1, expCol = -1, packCol = -1, hsnCol = -1, discCol = -1;
            int cgstCol = -1, sgstCol = -1, igstCol = -1;

            int headerRowIndex = -1;

            foreach (var row in sheet.RowsUsed().Take(20))
            {
                var rowCells = row.Cells().ToList();
                for (int i = 0; i < rowCells.Count; i++)
                {
                    var val = rowCells[i].Value.ToString().Trim();
                    var lower = val.ToLowerInvariant();

                    if (supplier == "" && (lower.Contains("supplier") || lower.Contains("vendor") || lower.Contains("m/s")))
                    {
                        supplier = val;
                    }
                    if (billNo == "" && (lower.Contains("inv") || lower.Contains("bill") || lower.Contains("invoice")))
                    {
                        billNo = Regex.Match(val, @"[A-Za-z0-9/-]{3,}").Value;
                    }

                    if (lower.Contains("product") || lower.Contains("item") || lower.Contains("description") || lower.Contains("particulars"))
                    {
                        headerRowIndex = row.RowNumber();
                        nameCol = i + 1;
                    }
                    else if (lower.Contains("qty") || lower.Contains("quantity")) qtyCol = i + 1;
                    else if (lower.Contains("cost") || lower.Contains("buy") || lower.Contains("purchase")) costCol = i + 1;
                    else if (lower.Contains("rate") || lower.Contains("price")) rateCol = i + 1;
                    else if (lower.Contains("mrp")) mrpCol = i + 1;
                    else if (lower.Contains("batch")) batchCol = i + 1;
                    else if (lower.Contains("exp") || lower.Contains("expiry")) expCol = i + 1;
                    else if (lower.Contains("pack")) packCol = i + 1;
                    else if (lower.Contains("hsn")) hsnCol = i + 1;
                    else if (lower.Contains("disc")) discCol = i + 1;
                    else if (lower == "cgst") cgstCol = i + 1;
                    else if (lower == "sgst") sgstCol = i + 1;
                    else if (lower == "igst" || lower.Contains("gst")) igstCol = i + 1;
                }
                if (headerRowIndex > 0) break;
            }

            if (headerRowIndex <= 0 || nameCol <= 0) return string.Empty;

            var products = new List<Dictionary<string, string>>();
            foreach (var row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRowIndex))
            {
                string pName = sheet.Cell(row.RowNumber(), nameCol).Value.ToString().Trim();
                if (string.IsNullOrWhiteSpace(pName)) continue;

                string qty = qtyCol > 0 ? sheet.Cell(row.RowNumber(), qtyCol).Value.ToString().Trim() : "1";
                string cost = costCol > 0 ? sheet.Cell(row.RowNumber(), costCol).Value.ToString().Trim() : "";
                string rate = rateCol > 0 ? sheet.Cell(row.RowNumber(), rateCol).Value.ToString().Trim() : "";
                string mrp = mrpCol > 0 ? sheet.Cell(row.RowNumber(), mrpCol).Value.ToString().Trim() : "";
                string batch = batchCol > 0 ? sheet.Cell(row.RowNumber(), batchCol).Value.ToString().Trim() : "";
                string exp = expCol > 0 ? sheet.Cell(row.RowNumber(), expCol).Value.ToString().Trim() : "";
                string pack = packCol > 0 ? sheet.Cell(row.RowNumber(), packCol).Value.ToString().Trim() : "";
                string hsn = hsnCol > 0 ? sheet.Cell(row.RowNumber(), hsnCol).Value.ToString().Trim() : "";
                string disc = discCol > 0 ? sheet.Cell(row.RowNumber(), discCol).Value.ToString().Trim() : "";
                string cgst = cgstCol > 0 ? sheet.Cell(row.RowNumber(), cgstCol).Value.ToString().Trim() : "";
                string sgst = sgstCol > 0 ? sheet.Cell(row.RowNumber(), sgstCol).Value.ToString().Trim() : "";
                string igst = igstCol > 0 ? sheet.Cell(row.RowNumber(), igstCol).Value.ToString().Trim() : "";

                if (string.IsNullOrEmpty(cost)) cost = rate;
                if (string.IsNullOrEmpty(rate)) rate = cost;

                var prodDict = new Dictionary<string, string>
                {
                    ["NAME"] = pName,
                    ["BATCH NO"] = batch,
                    ["EXPIRY"] = exp,
                    ["MRP"] = mrp,
                    ["RATE"] = rate,
                    ["COST"] = cost,
                    ["QTY"] = qty,
                    ["PACK"] = pack,
                    ["DISC"] = disc,
                    ["CGST"] = cgst,
                    ["SGST"] = sgst,
                    ["IGST"] = igst,
                    ["HSN"] = hsn,
                    ["CATEGORY"] = "",
                    ["MKT"] = "",
                    ["BONUS"] = ""
                };
                NormalizePackingInItem(prodDict);
                products.Add(prodDict);
            }

            if (products.Count == 0) return string.Empty;

            var result = new
            {
                SUPPLIER = supplier,
                BILL_NO = billNo,
                DATE = dateStr,
                products = products
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented).Replace("\"BILL_NO\"", "\"BILL NO\"");
        }

        private static void NormalizePackingInItem(Dictionary<string, string> item)
        {
            if (!item.TryGetValue("PACK", out var packStr) || string.IsNullOrWhiteSpace(packStr)) return;

            var pack = packStr.Trim();
            var matchNX = Regex.Match(pack, @"^(\d+)\*(\d+)$");
            var match1NX = Regex.Match(pack, @"^1\*(\d+)\*(\d+)$");

            int multiplierN = 0;
            if (match1NX.Success)
            {
                int.TryParse(match1NX.Groups[1].Value, out multiplierN);
            }
            else if (matchNX.Success)
            {
                var first = matchNX.Groups[1].Value;
                if (first != "1")
                {
                    int.TryParse(first, out multiplierN);
                }
            }

            if (multiplierN > 1)
            {
                if (item.TryGetValue("QTY", out var qtyStr) && decimal.TryParse(qtyStr, out var qtyVal) && qtyVal > 0)
                {
                    item["QTY"] = (qtyVal * multiplierN).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                foreach (var key in new[] { "MRP", "RATE", "COST" })
                {
                    if (item.TryGetValue(key, out var priceStr) && decimal.TryParse(priceStr, out var priceVal) && priceVal > 0)
                    {
                        var normPrice = Math.Round(priceVal / multiplierN, 4);
                        item[key] = normPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        private string ParseInvoiceTextNatively(string rawText)
        {
            string supplier = "";
            string billNo = "";
            string dateStr = "";

            var lines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Extract Header Details
            foreach (var line in lines.Take(25))
            {
                var l = line.Trim();

                if (string.IsNullOrEmpty(supplier))
                {
                    var supMatch = Regex.Match(l, @"(?:Supplier|Vendor|Seller|M/s|From)[\s:]+([^\r\n,;]+)", RegexOptions.IgnoreCase);
                    if (supMatch.Success) supplier = supMatch.Groups[1].Value.Trim();
                }

                if (string.IsNullOrEmpty(billNo))
                {
                    var billMatch = Regex.Match(l, @"(?:Bill\s*(?:No|#)|Invoice\s*(?:No|#)|Inv\s*(?:No|#))[\s:]*([A-Za-z0-9/-]+)", RegexOptions.IgnoreCase);
                    if (billMatch.Success) billNo = billMatch.Groups[1].Value.Trim();
                }

                if (string.IsNullOrEmpty(dateStr))
                {
                    var dateMatch = Regex.Match(l, @"\b(\d{1,2}[/-]\d{1,2}[/-]\d{2,4}|\d{4}[/-]\d{1,2}[/-]\d{1,2})\b");
                    if (dateMatch.Success) dateStr = dateMatch.Groups[1].Value;
                }
            }

            if (string.IsNullOrEmpty(supplier) && lines.Length > 0)
            {
                supplier = lines[0].Trim();
            }

            // Extract Products Table
            var products = new List<Dictionary<string, string>>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length < 6) continue;

                var lower = line.ToLowerInvariant();
                if (lower.Contains("total") || lower.Contains("subtotal") || lower.Contains("gstin") || lower.Contains("terms") || lower.Contains("thank you")) continue;

                var tokens = line.Split(new[] { '\t', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList();

                if (tokens.Count < 2)
                {
                    tokens = Regex.Split(line, @"\s{2,}")
                                  .Select(t => t.Trim())
                                  .Where(t => !string.IsNullOrEmpty(t))
                                  .ToList();
                }

                if (tokens.Count < 2) continue;

                string pName = "";
                string qty = "1";
                string cost = "";
                string rate = "";
                string mrp = "";
                string batch = "";
                string expiry = "";
                string hsn = "";
                string pack = "";

                for (int i = 0; i < tokens.Count; i++)
                {
                    var tok = tokens[i];

                    if (Regex.IsMatch(tok, @"^\d+\*\d+(\*\d+)?$") && string.IsNullOrEmpty(pack))
                    {
                        pack = tok;
                        continue;
                    }

                    if (Regex.IsMatch(tok, @"^\d{4,8}$") && string.IsNullOrEmpty(hsn))
                    {
                        hsn = tok;
                        continue;
                    }

                    if (Regex.IsMatch(tok, @"^(0[1-9]|1[0-2])[/-](\d{2}|\d{4})$") && string.IsNullOrEmpty(expiry))
                    {
                        expiry = tok;
                        continue;
                    }

                    if (Regex.IsMatch(tok, @"^[A-Za-z0-9]{3,12}$") && !Regex.IsMatch(tok, @"^\d+$") && string.IsNullOrEmpty(batch) && i > 0)
                    {
                        batch = tok;
                        continue;
                    }

                    if (decimal.TryParse(tok, out var numValue))
                    {
                        if (numValue > 0 && numValue < 10000 && string.IsNullOrEmpty(cost))
                        {
                            cost = tok;
                            rate = tok;
                        }
                        else if (numValue >= 1 && numValue <= 50000 && string.IsNullOrEmpty(mrp) && !string.IsNullOrEmpty(cost))
                        {
                            mrp = tok;
                        }
                        else if (numValue > 0 && numValue < 1000 && qty == "1" && i > 0)
                        {
                            qty = tok;
                        }
                    }
                    else if (tok.Length > 2 && string.IsNullOrEmpty(pName))
                    {
                        pName = tok;
                    }
                }

                if (!string.IsNullOrWhiteSpace(pName) && pName.Length >= 3 && !Regex.IsMatch(pName, @"^(Page|Date|Invoice|Bill|Total|GST)", RegexOptions.IgnoreCase))
                {
                    var prodDict = new Dictionary<string, string>
                    {
                        ["NAME"] = pName,
                        ["BATCH NO"] = batch,
                        ["EXPIRY"] = expiry,
                        ["MRP"] = mrp,
                        ["RATE"] = rate,
                        ["COST"] = cost,
                        ["QTY"] = qty,
                        ["PACK"] = pack,
                        ["DISC"] = "",
                        ["CGST"] = "",
                        ["SGST"] = "",
                        ["IGST"] = "",
                        ["HSN"] = hsn,
                        ["CATEGORY"] = "",
                        ["MKT"] = "",
                        ["BONUS"] = ""
                    };
                    NormalizePackingInItem(prodDict);
                    products.Add(prodDict);
                }
            }

            var result = new
            {
                SUPPLIER = supplier,
                BILL_NO = billNo,
                DATE = dateStr,
                products = products
            };

            return JsonConvert.SerializeObject(result, Formatting.Indented).Replace("\"BILL_NO\"", "\"BILL NO\"");
        }

        public async Task<AiActionResponse> ProcessRawTextWithAiAsync(string rawText)
        {
            var prompt = "Following is the raw text extracted from a business document (PDF/Excel). Please identify all products/items, their quantities, cost prices, and any other available details. Map them into the action 'extract_file_data' with the structured data. If it looks like a purchase invoice, map it to 'create_purchase' data structure.";
            return await _aiService.GetAiActionAsync($"{prompt}\n\nRAW TEXT:\n{rawText}");
        }
    }
}
