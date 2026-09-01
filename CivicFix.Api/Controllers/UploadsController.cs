using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace CivicFix.Api.Controllers
{
   
    [ApiController]
    [Route("api/[controller]")] // base address: api/Uploads
    public class UploadsController : ControllerBase
    {
        // IWebHostEnvironment is given to us by .NET. It knows where the project
        // folder is on disk, which is how we find the wwwroot folder to save into.
        private readonly IWebHostEnvironment _environment;

        public UploadsController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }
        private static readonly string[] AllowedExtensions =
            new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };// only these image types are accepted — never trust the file the browser sends

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        [Authorize(Roles = "Resident,Staff,Admin")] // must be logged in to upload
        [HttpPost] // address: POST api/Uploads
        [RequestSizeLimit(MaxFileSizeBytes)] // stops a huge file before it is even read
        public async Task<IActionResult> UploadPhoto(IFormFile file)//Iform represent the phtoto that react send
        {// Step 1 — was anything actually sent?
            if (file == null || file.Length == 0)//null nothing sended , equal zero means empty
                return BadRequest("No file was uploaded.");

            // Step 2 — size check.
            if (file.Length > MaxFileSizeBytes)
                return BadRequest("The photo is too large. Maximum size is 5 MB.");

            // Step 3 — extension check.
            // This is a security step, not a convenience one: without it someone
            // could upload a .exe or a .html file and the server would happily
            // host it for anyone to download.
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                return BadRequest($"Only image files are allowed ({string.Join(", ", AllowedExtensions)}).");

            // Step 4 — work out where wwwroot is.
            var webRootPath = _environment.WebRootPath;//ASP.NET has a special folder called

            if (string.IsNullOrEmpty(webRootPath))
                webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");//If ASP.NET didn't give me a wwwroot path, I'll construct one myself.

            var uploadsFolder = Path.Combine(webRootPath, "uploads");

            // creates the folder if it is missing, and does nothing if it already exists
            Directory.CreateDirectory(uploadsFolder);///create the uploads folder

            // Step 5 — invent a brand new file name.
            //
            // We deliberately DO NOT keep the user's original file name. Two reasons:
            //   - two people uploading "photo.jpg" would overwrite each other
            //   - a crafted file name like "..\..\appsettings.json" could otherwise
            //     write outside the uploads folder (a path traversal attack)
            // A Guid is unique and contains no user-controlled characters.
            var safeFileName = $"{Guid.NewGuid()}{extension}";//save file in another way: bgdfgdsfs
            var fullPath = Path.Combine(uploadsFolder, safeFileName);//C:\Users\Win11\Desktop\CivicFix\wwwroot\uploads\a83f9c2e.jpg

            // Step 6 — write the bytes to disk.
            // System.IO.File is spelled out because ControllerBase already has a
            // method called File(), and the plain name would refer to that instead.
            using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            // Step 7 — build the public URL for the saved file.
            // app.UseStaticFiles() in Program.cs is what makes everything inside
            // wwwroot reachable over http, so wwwroot/uploads/abc.jpg is served at
            // http://localhost:5140/uploads/abc.jpg
            var url = $"{Request.Scheme}://{Request.Host}/uploads/{safeFileName}";

            return Ok(new { url = url, fileName = safeFileName });//front end then well recive :  so we can show the photo if we want
                                                                   //{"url": "http://localhost:5140/uploads/abc123.jpg",
                                                                    //"fileName": "abc123.jpg"}

        }//Receive a photo from React → check it → save it inside wwwroot/uploads → return a URL that React can use.
    }
}
