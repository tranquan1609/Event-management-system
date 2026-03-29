using Microsoft.AspNetCore.Mvc;
using DACSWEBSK.Models;
using DACSWEBSK.Service.Bot;
using System.Threading.Tasks;
using DACSWEBSK.Services;

namespace DACSWEBSK.Controllers
{
    [ApiController]
    [Route("api/simplechat")]
    public class SimpleChatController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OpenAIService _openAIService;

        public SimpleChatController(ApplicationDbContext context, OpenAIService openAIService)
        {
            _context = context;
            _openAIService = openAIService;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ChatRequest req)
        {
            var bot = new DacsBot(_context, _openAIService);
            var response = await bot.GetBotResponse(req.text);
            return Ok(new { text = response });
        }
    }

    public class ChatRequest
    {
        public string text { get; set; }
    }
} 