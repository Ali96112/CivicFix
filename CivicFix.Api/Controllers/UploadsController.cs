using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CivicFix.Api.Controllers
{
   
    [ApiController]
    [Route("api/[controller]")]
    public class UploadsController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public UploadsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }
        private static readonly string[] AllowedExtensions =
            new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        [Authorize(Roles = "Resident,Staff,Admin")]
        [HttpPost]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file was uploaded.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest("The photo is too large. Maximum size is 5 MB.");

            
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                return BadRequest($"Only image files are allowed ({string.Join(", ", AllowedExtensions)}).");

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrEmpty(webRootPath))
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");

            var uploadsFolder = Path.Combine(webRootPath, "uploads");

            Directory.CreateDirectory(uploadsFolder);

       
            var safeFileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsFolder, safeFileName);

            
            using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"{Request.Scheme}://{Request.Host}/uploads/{safeFileName}";

            return Ok(new { url = url, fileName = safeFileName });

        }
    }
}
