using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DACSWEBSK.Models;
using Microsoft.EntityFrameworkCore;
using DACSWEBSK.Services;

namespace DACSWEBSK.Service.Bot
{
    public class DacsBot : ActivityHandler
    {
        private readonly ApplicationDbContext _context;
        private readonly OpenAIService _openAIService;

        public DacsBot(ApplicationDbContext context, OpenAIService openAIService)
        {
            _context = context;
            _openAIService = openAIService;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var userMessage = turnContext.Activity.Text.ToLower();
            string response = await GetBotResponse(userMessage);

            await turnContext.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
        }

        public async Task<string> GetBotResponse(string userMessage)
        {
            try
            {
                // Lưu tin nhắn của user
                var chatMessage = new ChatMessage
                {
                    Message = userMessage,
                    IsFromUser = true,
                    UserId = "system" // Sẽ cập nhật sau khi có user authentication
                };
                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                string response = null;

                if (userMessage.Contains("sự kiện") || userMessage.Contains("event"))
                {
                    List<Event> events = null;
                    string eventType = "";

                    // Tìm tên địa điểm trong câu hỏi (ví dụ: "tại thủ đức", "ở tphcm")
                    string locationKeyword = null;
                    var locationPrefixes = new[] { "tại ", "ở ", "in " };
                    foreach (var prefix in locationPrefixes)
                    {
                        int idx = userMessage.IndexOf(prefix);
                        if (idx != -1)
                        {
                            locationKeyword = userMessage.Substring(idx + prefix.Length).Split(new[] { '.', ',', '?', '!' }, 2)[0].Trim();
                            break;
                        }
                    }

                    // Trích xuất thời gian từ câu hỏi
                    DateTime? fromDate = null, toDate = null;
                    var match = System.Text.RegularExpressions.Regex.Match(userMessage, @"từ (\d{1,2}/\d{1,2}/\d{4}) đến (\d{1,2}/\d{1,2}/\d{4})");
                    if (match.Success)
                    {
                        fromDate = DateTime.ParseExact(match.Groups[1].Value, "dd/MM/yyyy", null);
                        toDate = DateTime.ParseExact(match.Groups[2].Value, "dd/MM/yyyy", null);
                    }
                    else
                    {
                        match = System.Text.RegularExpressions.Regex.Match(userMessage, @"ngày (\d{1,2}/\d{1,2}/\d{4})");
                        if (match.Success)
                        {
                            fromDate = toDate = DateTime.ParseExact(match.Groups[1].Value, "dd/MM/yyyy", null);
                        }
                        match = System.Text.RegularExpressions.Regex.Match(userMessage, @"tháng (\d{1,2})/(\d{4})");
                        if (match.Success)
                        {
                            int month = int.Parse(match.Groups[1].Value);
                            int year = int.Parse(match.Groups[2].Value);
                            fromDate = new DateTime(year, month, 1);
                            toDate = fromDate.Value.AddMonths(1).AddDays(-1);
                        }
                    }

                    // Query cơ bản
                    var query = _context.Events
                        .Include(e => e.Location)
                        .AsQueryable();

                    // Lọc theo địa điểm nếu có
                    if (!string.IsNullOrEmpty(locationKeyword))
                    {
                        query = query.Where(e => e.Location != null && e.Location.Name.ToLower().Contains(locationKeyword.ToLower()));
                    }

                    // Lọc theo thời gian nếu có
                    if (fromDate.HasValue && toDate.HasValue)
                    {
                        query = query.Where(e => e.StartTime <= toDate && e.EndTime >= fromDate);
                        eventType = $"từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}";
                    }
                    else if (userMessage.Contains("đang diễn ra") || userMessage.Contains("happening now") || userMessage.Contains("đang diễn"))
                    {
                        query = query.Where(e => e.StartTime <= DateTime.Now && e.EndTime >= DateTime.Now);
                        eventType = "đang diễn ra";
                    }
                    else if (userMessage.Contains("đã diễn ra") || userMessage.Contains("past") || userMessage.Contains("kết thúc"))
                    {
                        query = query.Where(e => e.EndTime < DateTime.Now);
                        eventType = "đã diễn ra";
                    }
                    else if (userMessage.Contains("tất cả") || userMessage.Contains("all"))
                    {
                        eventType = "tất cả";
                    }
                    else
                    {
                        query = query.Where(e => e.StartTime > DateTime.Now);
                        eventType = "sắp diễn ra";
                    }

                    // Lấy kết quả
                    events = await query.OrderBy(e => e.StartTime).Take(10).ToListAsync();

                    if (events.Any())
                    {
                        string eventList = "";
                        foreach (var evt in events)
                        {
                            eventList += $"- {evt.Title}: {evt.StartTime:dd/MM/yyyy} đến {evt.EndTime:dd/MM/yyyy}\n";
                        }
                        var aiResponse = await _openAIService.GetCompletionAsync(
                            $"Dưới đây là các sự kiện {eventType}:\n{eventList}\nHãy trả lời cho người dùng một cách thân thiện, tự nhiên, giới thiệu các sự kiện này."
                        );
                        if (aiResponse.Contains("quá tải") || aiResponse.Contains("thử lại"))
                        {
                            return $"Các sự kiện {eventType}:\n{eventList}";
                        }
                        return aiResponse;
                    }
                    else
                    {
                        var aiResponse = await _openAIService.GetCompletionAsync(
                            $"Hiện tại không có sự kiện {eventType}. Hãy trả lời cho người dùng một cách thân thiện, tự nhiên."
                        );
                        if (aiResponse.Contains("quá tải") || aiResponse.Contains("thử lại"))
                        {
                            return $"Hiện tại không có sự kiện {eventType}.";
                        }
                        return aiResponse;
                    }
                }
                else if (userMessage.Contains("quà") || userMessage.Contains("gift"))
                {
                    // Giả sử user có 100 điểm (bạn có thể thay bằng điểm thực tế của user nếu có)
                    int userPoints = 100;

                    List<Gift> gifts = null;
                    string giftType = "";

                    if (userMessage.Contains("đủ điểm") || userMessage.Contains("có thể đổi") || userMessage.Contains("có thể nhận"))
                    {
                        gifts = await _context.Gifts
                            .Where(g => g.RequiredPoints != null && g.RequiredPoints > 0 && g.RequiredPoints <= userPoints)
                            .Take(10)
                            .ToListAsync();
                        giftType = $"bạn đã đủ {userPoints} điểm để đổi";
                    }
                    else
                    {
                        gifts = await _context.Gifts
                            .Where(g => g.RequiredPoints != null && g.RequiredPoints > 0)
                            .Take(10)
                            .ToListAsync();
                        giftType = "hiện có";
                    }

                    if (gifts.Any())
                    {
                        string giftList = "";
                        foreach (var gift in gifts)
                        {
                            giftList += $"- {gift.Name}: {gift.RequiredPoints} điểm đổi\n";
                        }
                        var aiResponse = await _openAIService.GetCompletionAsync(
                            $"Dưới đây là các quà tặng {giftType}:\n{giftList}\nHãy trả lời cho người dùng một cách thân thiện, tự nhiên, giới thiệu các quà tặng này."
                        );
                        if (aiResponse.Contains("quá tải") || aiResponse.Contains("thử lại"))
                        {
                            return $"Các quà tặng {giftType}:\n{giftList}";
                        }
                        return aiResponse;
                    }
                    else
                    {
                        var aiResponse = await _openAIService.GetCompletionAsync(
                            $"Hiện tại không có quà tặng {giftType}. Hãy trả lời cho người dùng một cách thân thiện, tự nhiên."
                        );
                        if (aiResponse.Contains("quá tải") || aiResponse.Contains("thử lại"))
                        {
                            return $"Hiện tại không có quà tặng {giftType}.";
                        }
                        return aiResponse;
                    }
                }
                else if (userMessage.Contains("địa điểm") || userMessage.Contains("location"))
                {
                    var locations = await _context.Locations.Take(3).ToListAsync();
                    if (locations.Any())
                    {
                        string locationList = "";
                        foreach (var location in locations)
                        {
                            locationList += $"- {location.Name}: {location.Address}\n";
                        }
                        response = await _openAIService.GetCompletionAsync(
                            $"Dưới đây là các địa điểm:\n{locationList}\nHãy trả lời cho người dùng một cách thân thiện, tự nhiên, giới thiệu các địa điểm này."
                        );
                    }
                    else
                    {
                        response = await _openAIService.GetCompletionAsync(
                            "Không có thông tin về địa điểm. Hãy trả lời cho người dùng một cách thân thiện, tự nhiên."
                        );
                    }
                }

                // Nếu không khớp rule nào, gọi OpenAI như cũ
                if (string.IsNullOrEmpty(response))
                {
                    response = await _openAIService.GetCompletionAsync(
                        $"Bạn là trợ lý sự kiện, hãy trả lời tự nhiên, thân thiện cho câu hỏi: {userMessage}"
                    );
                }

                // Lưu phản hồi của bot
                var botResponse = new ChatMessage
                {
                    Message = response,
                    IsFromUser = false,
                    UserId = "system"
                };
                _context.ChatMessages.Add(botResponse);
                await _context.SaveChangesAsync();

                return response;
            }
            catch (Exception ex)
            {
                // Ghi log ra file hoặc console
                System.IO.File.AppendAllText("bot_error.log", ex.ToString());
                return "Đã xảy ra lỗi nội bộ: " + ex.Message;
            }
        }
    }
} 