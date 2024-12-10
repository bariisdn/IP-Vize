using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FileManagement.Data;
using FileManagement.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using File = FileManagement.Models.File;

namespace FileManagement.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class FilesController(IRepository<File> fileRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetFiles()
    {
        var files = await fileRepository.GetAllAsync();

        // Döngüsel referanslardan kaçınmak için basitleştirilmiş bir nesne döndür
        var result = files.Select(file => new
        {
            file.Id,
            file.Name,
            file.FileType,
            file.UploadedOn,
            file.UserId,
            UserName = file.User?.Username // Null kontrolü yapılabilir
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetFile(int id)
    {
        var file = await fileRepository.GetByIdAsync(id);
        if (file == null)
            return NotFound();

        // Döngüsel referanslardan kaçınmak için basitleştirilmiş bir nesne döndür
        var result = new
        {
            file.Id,
            file.Name,
            file.FileType,
            file.UploadedOn,
            file.UserId,
            UserName = file.User?.Username // Null kontrolü yapılabilir
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult> AddFile(File file)
    {
        await fileRepository.AddAsync(file);
        return CreatedAtAction(nameof(GetFile), new { id = file.Id }, file);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> UpdateFile(int id, File file)
    {
        if (id != file.Id)
            return BadRequest();

        await fileRepository.UpdateAsync(file);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteFile(int id)
    {
        try
        {
            var file = await fileRepository.GetByIdAsync(id); // Silinecek dosyayı getir
            if (file == null)
            {
                return NotFound(new { message = "Dosya bulunamadı." });
            }

            await fileRepository.DeleteAsync(id); // Dosyayı sil
            return Ok(new { message = "Dosya başarıyla silindi." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata (DeleteFile): {ex.Message}"); // Hata loglama
            return StatusCode(500, new { message = "Dosya silinirken bir hata oluştu." });
        }
    }
}