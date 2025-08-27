using Microsoft.AspNetCore.Mvc;
using ABC_Retailers_ST10445830.Models;
using ABC_Retailers_ST10445830.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ABC_Retailers_ST10445830.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storageService;

        public UploadController(IAzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                    {
                        var fileName = await _storageService.UploadFileAsync(model.ProofOfPayment, "payment-proofs");

                        await _storageService.UploadToFileShareAsync(model.ProofOfPayment, "contracts", "payments");

                        TempData["Success"] = $"File uploaded successfully! File name: {fileName}";

                        return View(new FileUploadModel());
                    }
                    else
                    {
                        ModelState.AddModelError("ProofofPayment", "Please select a file to upload.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"An error occurred while uploading the file: {ex.Message}");
                }
            }
            return View(model);
        }
    }
}
