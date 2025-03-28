using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace PDFCompressorApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Compress(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                ViewBag.Message = "Please upload a valid PDF file.";
                return View("Index");
            }

            var safeFileName = Path.GetFileNameWithoutExtension(pdfFile.FileName).Replace(" ", "_") + Path.GetExtension(pdfFile.FileName);

            // Process in memory
            try
            {
                // Read the uploaded PDF into a memory stream
                using var inputStream = new MemoryStream();
                await pdfFile.CopyToAsync(inputStream);
                inputStream.Position = 0; // Reset for reading

                // Output compressed PDF to another memory stream
                using var outputStream = new MemoryStream();

                var gsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/gs/bin/gswin64c.exe");
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = gsPath;
                process.StartInfo.Arguments = $"-sDEVICE=pdfwrite -dCompatibilityLevel=1.4 -dPDFSETTINGS=/screen -dNOPAUSE -dQUIET -dBATCH -sOutputFile=- -"; // "-" means stdout
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                // Pipe input PDF to Ghostscript
                await inputStream.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();

                // Get compressed PDF from stdout
                await process.StandardOutput.BaseStream.CopyToAsync(outputStream);
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var originalSize = inputStream.Length / 1024; // KB
                    var compressedSize = outputStream.Length / 1024; // KB
                    ViewBag.Message = $"PDF compressed successfully! {originalSize} KB -> {compressedSize} KB";
                    return File(outputStream.ToArray(), "application/pdf", "compressed_" + safeFileName);
                }
                else
                {
                    ViewBag.Message = "Ghostscript failed. Exit code: " + process.ExitCode;
                    return View("Index");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Ghostscript Error: " + ex.Message;
                return View("Index");
            }
        }
    }
}