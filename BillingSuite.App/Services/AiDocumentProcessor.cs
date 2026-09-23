using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BillingSuite.App.Models;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Newtonsoft.Json;

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

        public async Task<AiActionResponse> ProcessRawTextWithAiAsync(string rawText)
        {
            var prompt = "Following is the raw text extracted from a business document (PDF/Excel). Please identify all products/items, their quantities, cost prices, and any other available details. Map them into the action 'extract_file_data' with the structured data. If it looks like a purchase invoice, map it to 'create_purchase' data structure.";
            return await _aiService.GetAiActionAsync($"{prompt}\n\nRAW TEXT:\n{rawText}");
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

            const string systemInstruction = @"You are an Invoice PDF/Excel Data Extraction Engine.
Your ONLY job is to read the uploaded invoice document completely and convert its contents into accurate, structured, import-ready JSON.

DO NOT:
- Ask what to do.
- Ask for confirmation.
- Add greetings or conversational text.
- Add markdown code fences like ```json.
- Omit any products. Extract EVERY product/item listed on every page.

Return ONLY valid JSON matching this exact schema:
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
- Every product object must contain all fields shown above.
- Every field value must be a string.
- QTY and PACK must be strings.
- PACK must preserve '*' separators exactly (e.g. 10*10).
- If field value is missing or unreadable, set value to """".
- Return ONLY valid JSON.";

            string prompt = $"DOCUMENT CONTENT:\n\n{rawText}\n\nExtract all invoice header details and all products into the specified JSON format.";
            string json = await _aiService.GenerateContentRawAsync(prompt, systemInstruction);

            json = json.Trim();
            if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                json = json.Substring(7);
            }
            else if (json.StartsWith("```"))
            {
                json = json.Substring(3);
            }
            if (json.EndsWith("```"))
            {
                json = json.Substring(0, json.Length - 3);
            }
            return json.Trim();
        }
    }
}
