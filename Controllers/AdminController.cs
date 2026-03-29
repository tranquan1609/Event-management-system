using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Services;

namespace DACSWEBSK.Controllers
{
    public class AdminController : Controller
    {
        private readonly CertificateTemplateService _templateService;

        public AdminController(CertificateTemplateService templateService)
        {
            _templateService = templateService;
        }

        [HttpGet]
        public IActionResult GenerateCertificateTemplate()
        {
            _templateService.GenerateDefaultTemplate();
            return RedirectToAction("Index", "Home");
        }
    }
} 