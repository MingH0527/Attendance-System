using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WebAndMobile.Controllers;

public class AdminController : Controller
{
    private readonly string _dataFile;

    public AdminController()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _dataFile = Path.Combine(dataDir, "timetable.json");
    }

    public IActionResult Index()
    {
        return Content("Welcome Admin");
    }

    public IActionResult Timetable()
    {
        return View();
    }
    [HttpGet]
    public IActionResult GetTimetableData()
    {
        if (!System.IO.File.Exists(_dataFile))
        {
            return Json(new Dictionary<string, CellData>());
        }

        var json = System.IO.File.ReadAllText(_dataFile);
        var data = JsonSerializer.Deserialize<Dictionary<string, CellData>>(json)
                   ?? new Dictionary<string, CellData>();
        return Json(data);
    }
    [HttpPost]
    public async Task<IActionResult> SaveTimetableData([FromBody] Dictionary<string, CellData> data)
    {
        if (data is null)
        {
            return BadRequest("数据格式错误");
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(_dataFile, json);
        return Ok(new { message = "已保存" });
    }
}

// 每个格子存的数据：老师 + 科目
public record CellData(string Teacher, string Subject);
