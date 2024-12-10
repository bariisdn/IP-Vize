using System.Security.Claims;
using FileManagement.Data;
using FileManagement.Models;
using FileManagement.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using File = FileManagement.Models.File;

namespace FileManagement.Controllers;
// /Controllers/AdminController.cs
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<File> _fileRepository;

    public AdminController(IRepository<User> userRepository, IRepository<File> fileRepository)
    {
        _userRepository = userRepository;
        _fileRepository = fileRepository;
    }

    public async Task<IActionResult> Users()
    {
        var usersList = await GetUsersList(); // Kullanıcı + Dosya sayısı
        return View(usersList); // Dosya sayısı tabloya eklenmiş bir şekilde döner
    }
    
    [HttpGet]
    public async Task<IActionResult> SearchUsers(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var allUsers = await _userRepository.GetAllAsync();
            return Json(allUsers);
        }

        var filteredUsers = await _userRepository.GetAllAsync();
        var result = filteredUsers
            .Where(u =>
                u.Username.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Json(result);
    }
    // Kullanıcı Listeleme

    // Kullanıcı Ekleme
    [HttpPost]
    public async Task<IActionResult> AddUser([FromBody] RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);  // Model doğrulama hatalarını döndür

        var user = new User
        {
            Username = model.Username,
            Email = model.Email,
            Role = model.Role,
            CreatedOn = DateTime.Now,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password) // Şifreyi hash'leyerek sakla
        };

        await _userRepository.AddAsync(user);
        return Ok(new { message = "Kullanıcı başarıyla eklendi." });
    }

    // Kullanıcı Güncelleme
    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        var model = new EditUserViewModel()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        };

        return Json(model); // Düzenleme için modal verisini döner
    }


    [HttpPut]
    public async Task<IActionResult> EditUser([FromBody] EditUserViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userRepository.GetByIdAsync(model.Id);

        user.Username = model.Username;
        user.Email = model.Email;
        user.Role = model.Role;

        await _userRepository.UpdateAsync(user);
        return Ok(new { message = "Kullanıcı başarıyla güncellendi." });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Kullanıcı bulunamadı." });
            }

            // Sadece ID gönderin
            await _userRepository.DeleteAsync(id);
            return Ok(new { message = "Kullanıcı başarıyla silindi." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata: {ex.Message}");
            return StatusCode(500, new { message = "Kullanıcı silinirken bir hata oluştu." });
        }
    }
    // Kullanıcıları dosya sayısıyla birlikte getiren metod
    private async Task<IEnumerable<UsersListViewModel>> GetUsersList()
    {
        var users = await _userRepository.GetAllAsync();
        var fileCounts = await _fileRepository.GetAllAsync();

        // Kullanıcı başına dosya sayısını hesaplar
        var usersList = users.Select(user => new UsersListViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            CreatedOn = user.CreatedOn,
            FileCount = fileCounts.Count(file => file.UserId == user.Id) // Dosya sayısını ilişkilendir
        });

        return usersList;
    }
    
  
    
  
    // Kullanıcı işlemleri DONE
    public async Task<IActionResult> Files()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the logged-in user's ID
        ViewBag.UserId = userId;
        Console.WriteLine("KULLANICI ID" + userId);
        if (userId == null)
        {
            // Handle case where user ID is not found, e.g., if the user is not authenticated
            Console.WriteLine("Kullanıcı girişi tespit edilemedi"); // Or any other appropriate response
          
        }

        // Tüm dosyaları ve ilişkili kullanıcıyı alıyoruz
        //var files = await _fileRepository.GetAllAsync();
        var files = await _fileRepository.GetAllAsync(); // İlişkili kullanıcı bilgilerini dahil et

        // Kullanıcıların dosya sayısını hesaplamak için bir kullanıcı dizisi oluşturuyoruz
        var usersFileCount = files
            .GroupBy(f => f.UserId)
            .Select(group => new 
            {
                UserId = group.Key,
                FileCount = group.Count()
            })
            .ToList();

        // Map File to FileListViewModel
        var fileViewModels = files.Select(f => new FileListViewModel
        {
            Id = f.Id,
            FileName = f.Name,
            FileType = f.FileType,
            UploadedOn = f.UploadedOn,
            UserId = f.UserId,
            UserName = f.User != null ? f.User.Username : "Nan", // Null kontrolü
        }).ToList();

        return View(fileViewModels);  // ViewModel'i View'a gönderiyoruz
    }
    // Dosya Listeleme

    public async Task<IEnumerable<FileListViewModel>> FileList()
    {
        var files = await _fileRepository.GetAllAsync();

        var fileViewModels = files.Select(f => new FileListViewModel
        {
            Id = f.Id,
            FileName = f.Name,
            FileType = f.FileType,
            UploadedOn = f.UploadedOn,
            UserId = f.UserId,
            UserName = f.User.Username // Bu satırda sorun olabilir
        });

        return fileViewModels; // Pass the mapped ViewModel to the view
    }
    
   
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Lütfen geçerli bir dosya seçin." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get the logged-in user's ID
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { message = "Kullanıcı girişi tespit edilemedi." });
        }

        // Convert the string userId to an integer if needed
        if (!int.TryParse(userId, out int parsedUserId))
        {
            return BadRequest(new { message = "Geçersiz kullanıcı ID." });
        }

        var user = await _userRepository.GetByIdAsync(parsedUserId); // Assuming _userRepository is injected
        if (user == null)
        {
            return BadRequest(new { message = "Kullanıcı bulunamadı." });
        }

        ViewBag.UserId = userId;
        var fileEntity = new File
        {
            Name = Path.GetFileName(file.FileName),
            FileType = file.ContentType,
            UploadedOn = DateTime.Now,
            UserId = parsedUserId, // Use the parsed integer userId
            User = user,
            Content = new byte[] { } // Placeholder for file content
        };

        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileEntity.Content = memoryStream.ToArray(); // Save file content
        }

        await _fileRepository.AddAsync(fileEntity);

        return Ok(new { message = "Dosya başarıyla yüklendi." });
    }
    
    
    
    [HttpGet("File/Download/{id}")]
    [ApiExplorerSettings(IgnoreApi = true)] 
    public async Task<IActionResult> DownloadFile(int id)
    {
        var file = await _fileRepository.GetByIdAsync(id);
        if (file == null)
        {
            return NotFound(new { message = "Dosya bulunamadı." });
        }
        if (file.Content == null || file.Content.Length == 0)
        {
            return BadRequest(new { message = "Dosya içeriği boş." });
        }
        return File(file.Content, file.FileType ?? "application/octet-stream", file.Name);
    }
  
// FileType detection in File class
    public string GetFileType(byte[] content)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (provider.TryGetContentType("file", out string contentType))
        {
            return contentType;
        }

        // If detection fails, return a default type
        return "application/octet-stream";
    }

    // Dosya Silme
    
    [HttpDelete("File/Delete/{id}")]
    [ApiExplorerSettings(IgnoreApi = true)] 
    public async Task<IActionResult> DeleteFile(int id)
    {
        try
        {
            var file = await _fileRepository.GetByIdAsync(id);
            if (file == null)
            {
                return NotFound(new { message = "Dosya bulunamadı." });
            }
            await _fileRepository.DeleteAsync(id);
            return Ok(new { message = "Dosya başarıyla silindi." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (Dosya Silme): {ex.Message}");
            return StatusCode(500, new { message = "Dosya silinirken bir hata oluştu." });
        }
    }
    // Erişim İzni Hatası
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    
    // KATEGORİ İLE İLGİLİ İŞLEMLER
    
    
}