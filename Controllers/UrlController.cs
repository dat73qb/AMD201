using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Data;
using UrlShortener.Services;
using UrlShortener.DTOs;
using UrlShortener.Models; 

[Route("api/[controller]")]
[ApiController]
public class UrlController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IShortUrlService _shortUrlService;
    private readonly IConfiguration _configuration;

    public UrlController(
        ApplicationDbContext context,
        IShortUrlService shortUrlService,
        IConfiguration configuration)
    {
        _context = context;
        _shortUrlService = shortUrlService;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<UrlResponseDto>> CreateShortUrl([FromBody] UrlRequestDto request)
    {
        if (!_shortUrlService.IsValidUrl(request.Url))
        {
            return BadRequest("Invalid URL format");
        }

        string shortCode;
        do
        {
            shortCode = _shortUrlService.GenerateUniqueCode();
        } while (await _context.ShortUrls.AnyAsync(x => x.ShortCode == shortCode));

        var shortUrl = new ShortUrl
        {
            OriginalUrl = request.Url,
            ShortCode = shortCode,
            CreatedAt = DateTime.UtcNow,
            ClickCount = 0
        };

        _context.ShortUrls.Add(shortUrl);
        await _context.SaveChangesAsync();

        var baseUrl = _configuration["BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
        var response = new UrlResponseDto
        {
            OriginalUrl = shortUrl.OriginalUrl,
            ShortUrl = $"{baseUrl}/api/Url/{shortUrl.ShortCode}"
        };

        return Ok(response);
    }


    [HttpGet("{code}")]
    public async Task<IActionResult> RedirectToOriginal(string code)
    {
        var shortUrl = await _context.ShortUrls
            .FirstOrDefaultAsync(x => x.ShortCode == code);

        if (shortUrl == null)
        {
            return NotFound("Short URL not found");
        }

        shortUrl.ClickCount++;
        await _context.SaveChangesAsync();

        return Redirect(shortUrl.OriginalUrl);
    }
}
