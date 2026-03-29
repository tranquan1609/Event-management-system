using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace DACSWEBSK.Services
{
    public class HuggingFaceService
    {
        private readonly string _apiKey;
        private readonly string _model;
        private readonly ILogger<HuggingFaceService> _logger;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private const int MaxRetries = 3;
        private const int RetryDelaySeconds = 5;

        public HuggingFaceService(IConfiguration config, ILogger<HuggingFaceService> logger)
        {
            _apiKey = config["HuggingFace:ApiKey"] ?? throw new ArgumentNullException("HuggingFace:ApiKey", "API key is not configured");
            _model = config["HuggingFace:Model"] ?? throw new ArgumentNullException("HuggingFace:Model", "Model name is not configured");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Tăng timeout

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<string> SummarizeAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for summarization");
                return "Văn bản trống không thể tóm tắt.";
            }

            // Thử lại với retry mechanism
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Starting summarization attempt {Attempt}/{MaxRetries} for text length: {Length}", 
                        attempt, MaxRetries, text.Length);

                    var requestData = new
                    {
                        inputs = text,
                        parameters = new
                        {
                            max_length = 150,
                            min_length = 30,
                            do_sample = false,
                            temperature = 0.7,
                            top_p = 0.95
                        }
                    };

                    // Sử dụng endpoint mới của Hugging Face
                    // Format đúng: https://router.huggingface.co/hf-inference/models/{model}
                    var urls = new[]
                    {
                        $"https://router.huggingface.co/hf-inference/models/{_model}", // Router endpoint đúng format
                        $"https://api-inference.huggingface.co/models/{_model}", // Endpoint cũ (fallback)
                    };

                    HttpResponseMessage? response = null;
                    string responseContent = "";
                    string? usedUrl = null;

                    foreach (var url in urls)
                    {
                        try
                        {
                            _logger.LogInformation("Trying HuggingFace API endpoint: {Url}", url);
                            
                            response = await _httpClient.PostAsJsonAsync(url, requestData, _jsonOptions);
                            responseContent = await response.Content.ReadAsStringAsync();
                            usedUrl = url;
                            
                            _logger.LogInformation("Response from {Url}: Status {StatusCode}, Content length: {Length}", 
                                url, response.StatusCode, responseContent.Length);
                            
                            // Nếu thành công, dừng thử các endpoint khác
                            if (response.IsSuccessStatusCode)
                            {
                                break;
                            }
                            
                            // Nếu là lỗi "Gone" hoặc "Not Found", tiếp tục thử endpoint khác
                            if (response.StatusCode == System.Net.HttpStatusCode.Gone || 
                                response.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                continue;
                            }
                            
                            // Các lỗi khác, dừng thử
                            break;
                        }
                        catch (HttpRequestException ex)
                        {
                            _logger.LogWarning("Failed to connect to {Url}: {Error}", url, ex.Message);
                            continue;
                        }
                    }

                    if (response == null || string.IsNullOrWhiteSpace(usedUrl))
                    {
                        _logger.LogError("Failed to connect to all HuggingFace API endpoints");
                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelaySeconds * 1000);
                            continue;
                        }
                        return "Không thể kết nối đến dịch vụ tóm tắt. Vui lòng kiểm tra kết nối mạng hoặc API key.";
                    }

                    // Kiểm tra status code trước khi parse
                    if (!response.IsSuccessStatusCode)
                    {
                        // Xử lý các status code lỗi
                        if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            _logger.LogError("Endpoint no longer supported. Response: {Content}", responseContent);
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "Endpoint API đã thay đổi. Vui lòng liên hệ quản trị viên để cập nhật.";
                        }
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogError("Model or endpoint not found. Model: {Model}, URL: {Url}", _model, usedUrl ?? "unknown");
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "Model hoặc endpoint không tìm thấy. Vui lòng kiểm tra cấu hình model hoặc API key.";
                        }
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogError("Unauthorized - API key may be invalid or expired");
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "API key không hợp lệ hoặc đã hết hạn. Vui lòng kiểm tra cấu hình.";
                        }
                        
                        // Các lỗi khác
                        _logger.LogError("HuggingFace API returned error status: {StatusCode}. Response: {Content}", 
                            response.StatusCode, 
                            responseContent.Length > 500 ? responseContent.Substring(0, 500) : responseContent);
                        
                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelaySeconds * 1000);
                            continue;
                        }
                        return $"Không thể tạo tóm tắt. Lỗi: {response.StatusCode}";
                    }

                    // Xử lý response content trước khi parse
                    if (string.IsNullOrWhiteSpace(responseContent))
                    {
                        _logger.LogWarning("Empty response content from HuggingFace API");
                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelaySeconds * 1000);
                            continue;
                        }
                        return "Không thể tạo tóm tắt do phản hồi trống từ API.";
                    }

                    // Log response content để debug
                    _logger.LogInformation("HuggingFace API response (first 1000 chars): {Content}", 
                        responseContent.Length > 1000 ? responseContent.Substring(0, 1000) : responseContent);

                    // Parse response linh hoạt - chỉ parse nếu là JSON hợp lệ
                    JsonNode? jsonNode = null;
                    try
                    {
                        // Kiểm tra xem có phải JSON không (bắt đầu bằng { hoặc [)
                        var trimmedContent = responseContent.Trim();
                        if (trimmedContent.StartsWith("{") || trimmedContent.StartsWith("["))
                        {
                            jsonNode = JsonNode.Parse(responseContent);
                        }
                        else
                        {
                            // Nếu không phải JSON, có thể là string response
                            _logger.LogWarning("Response is not JSON format: {Content}", 
                                trimmedContent.Length > 200 ? trimmedContent.Substring(0, 200) : trimmedContent);
                            
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "Không thể tạo tóm tắt do định dạng phản hồi không hợp lệ từ API.";
                        }
                    }
                    catch (JsonException parseEx)
                    {
                        _logger.LogError(parseEx, "Failed to parse JSON response. Response content: {Content}", 
                            responseContent.Length > 2000 ? responseContent.Substring(0, 2000) + "..." : responseContent);
                        
                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelaySeconds * 1000);
                            continue;
                        }
                        return "Không thể xử lý phản hồi từ dịch vụ tóm tắt.";
                    }
                    
                    if (jsonNode == null)
                    {
                        _logger.LogWarning("JsonNode is null after parsing");
                        if (attempt < MaxRetries)
                        {
                            await Task.Delay(RetryDelaySeconds * 1000);
                            continue;
                        }
                        return "Không thể tạo tóm tắt do phản hồi không hợp lệ từ API.";
                    }
                    
                    // Kiểm tra nếu response chứa error message (ngay cả khi status code là 200)
                    if (jsonNode is JsonObject jsonObj)
                    {
                        // Kiểm tra nếu model đang loading
                        if (jsonObj.ContainsKey("error"))
                        {
                            var errorMessage = jsonObj["error"]?.ToString() ?? "Unknown error";
                            _logger.LogWarning("HuggingFace API returned error: {Error}", errorMessage);
                            
                            // Nếu model đang loading, đợi và thử lại
                            if (errorMessage.Contains("loading", StringComparison.OrdinalIgnoreCase) || 
                                errorMessage.Contains("is currently loading", StringComparison.OrdinalIgnoreCase))
                            {
                                int estimatedTime = 30; // Default
                                if (jsonObj.ContainsKey("estimated_time"))
                                {
                                    estimatedTime = jsonObj["estimated_time"]?.GetValue<int>() ?? 30;
                                }
                                
                                _logger.LogInformation("Model is loading, estimated time: {EstimatedTime}s. Waiting and retrying...", estimatedTime);
                                
                                if (attempt < MaxRetries)
                                {
                                    // Đợi lâu hơn một chút so với estimated time
                                    await Task.Delay((estimatedTime + 5) * 1000);
                                    continue;
                                }
                                else
                                {
                                    return "Model đang được tải. Vui lòng thử lại sau vài phút.";
                                }
                            }
                            
                            // Các lỗi khác
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return $"Không thể tạo tóm tắt: {errorMessage}";
                        }
                        
                        // Nếu response là object và có summary_text hoặc generated_text
                        if (jsonObj.ContainsKey("summary_text"))
                        {
                            var summary = jsonObj["summary_text"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(summary))
                            {
                                _logger.LogInformation("Successfully generated summary of length: {Length}", summary.Length);
                                return summary;
                            }
                        }
                        
                        if (jsonObj.ContainsKey("generated_text"))
                        {
                            var summary = jsonObj["generated_text"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(summary))
                            {
                                _logger.LogInformation("Successfully generated summary of length: {Length}", summary.Length);
                                return summary;
                            }
                        }
                    }
                    
                    // Xử lý response là array
                    if (jsonNode is JsonArray jsonArray && jsonArray.Count > 0)
                    {
                        var firstItem = jsonArray[0];
                        if (firstItem is JsonObject itemObj)
                        {
                            // Thử các key có thể có
                            string? summary = null;
                            
                            if (itemObj.ContainsKey("summary_text"))
                            {
                                summary = itemObj["summary_text"]?.ToString();
                            }
                            else if (itemObj.ContainsKey("generated_text"))
                            {
                                summary = itemObj["generated_text"]?.ToString();
                            }
                            else if (itemObj.ContainsKey("summary"))
                            {
                                summary = itemObj["summary"]?.ToString();
                            }
                            
                            if (!string.IsNullOrWhiteSpace(summary))
                            {
                                _logger.LogInformation("Successfully generated summary of length: {Length}", summary.Length);
                                return summary;
                            }
                        }
                    }

                    // Kiểm tra nếu response có status code không thành công
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogError("HuggingFace API returned error status: {StatusCode}. Response: {Content}", 
                            response.StatusCode, 
                            responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent);
                        
                        // Xử lý các status code cụ thể
                        if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                        {
                            _logger.LogError("Endpoint no longer supported. Response: {Content}", responseContent);
                            // Endpoint đã bị deprecated, không cần retry
                            return "Endpoint API đã thay đổi. Vui lòng liên hệ quản trị viên để cập nhật.";
                        }
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogError("Model or endpoint not found. Model: {Model}, URL: {Url}", _model, usedUrl ?? "unknown");
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "Model hoặc endpoint không tìm thấy. Vui lòng kiểm tra cấu hình model hoặc API key.";
                        }
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            _logger.LogError("Unauthorized - API key may be invalid or expired");
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "API key không hợp lệ hoặc đã hết hạn. Vui lòng kiểm tra cấu hình.";
                        }
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                        {
                            _logger.LogWarning("Service unavailable, will retry");
                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(RetryDelaySeconds * 1000);
                                continue;
                            }
                            return "Dịch vụ tạm thời không khả dụng. Vui lòng thử lại sau.";
                        }
                    }

                    // Nếu không parse được, log và thử lại
                    _logger.LogWarning("Unable to parse summary from response. Status: {StatusCode}, Response content: {Content}", 
                        response.StatusCode,
                        responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    
                    return "Không thể tạo tóm tắt do định dạng phản hồi không hợp lệ từ API.";
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Network error while calling HuggingFace API (attempt {Attempt}/{MaxRetries})", 
                        attempt, MaxRetries);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    return "Không thể kết nối đến dịch vụ tóm tắt. Vui lòng thử lại sau.";
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing response from HuggingFace API (attempt {Attempt}/{MaxRetries}). Exception: {Exception}", 
                        attempt, MaxRetries, ex.Message);
                    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                    
                    if (attempt < MaxRetries)
                    {
                        _logger.LogInformation("Retrying after {Delay} seconds...", RetryDelaySeconds);
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    
                    // Log chi tiết hơn ở lần thử cuối
                    _logger.LogError("Final attempt failed. This might indicate an API format change or invalid response.");
                    return "Không thể xử lý phản hồi từ dịch vụ tóm tắt. Vui lòng thử lại sau hoặc liên hệ quản trị viên.";
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogError(ex, "Timeout while calling HuggingFace API (attempt {Attempt}/{MaxRetries})", 
                        attempt, MaxRetries);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    return "Yêu cầu tóm tắt quá thời gian chờ. Vui lòng thử lại sau.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during summarization (attempt {Attempt}/{MaxRetries})", 
                        attempt, MaxRetries);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    return "Đã xảy ra lỗi không mong muốn khi tạo tóm tắt.";
                }
            }

            return "Không thể tạo tóm tắt sau nhiều lần thử.";
        }

        public async Task<string> SummarizeLongTextAsync(string text, int chunkSize = 400)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Văn bản trống không thể tóm tắt.";

            // Chỉ lấy phần đầu của văn bản (khoảng 1000 ký tự)
            var firstPart = text.Length > 1000 ? text.Substring(0, 1000) : text;
            
            // Tìm vị trí dấu chấm cuối cùng trong 1000 ký tự đầu
            int lastDot = firstPart.LastIndexOf('.');
            if (lastDot > 0)
            {
                firstPart = firstPart.Substring(0, lastDot + 1);
            }

            // Tóm tắt phần đầu
            return await SummarizeAsync(firstPart);
        }

        private List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            int start = 0;
            while (start < text.Length)
            {
                int length = Math.Min(maxChunkSize, text.Length - start);
                int lastDot = text.LastIndexOf('.', start + length - 1, length);
                if (lastDot >= start)
                {
                    length = lastDot - start + 1;
                }
                // Đảm bảo length > 0
                if (length <= 0) break;
                chunks.Add(text.Substring(start, length).Trim());
                start += length;
            }
            return chunks;
        }

        public string GetFirstSentences(string text, int numSentences = 3)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            int count = 0;
            int lastIndex = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '.')
                {
                    count++;
                    if (count == numSentences)
                    {
                        lastIndex = i + 1;
                        break;
                    }
                }
            }
            if (lastIndex == 0) lastIndex = text.Length;
            return text.Substring(0, lastIndex).Trim();
        }

        /// <summary>
        /// Dịch văn bản sang ngôn ngữ đích sử dụng HuggingFace Translation API
        /// </summary>
        /// <param name="text">Văn bản cần dịch</param>
        /// <param name="targetLanguage">Mã ngôn ngữ đích (vi, en, fr, de, es, ja, ko, zh, etc.)</param>
        /// <param name="sourceLanguage">Mã ngôn ngữ nguồn (mặc định: auto detect)</param>
        /// <returns>Văn bản đã dịch</returns>
        public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Empty text provided for translation");
                return "Văn bản trống không thể dịch.";
            }

            // Map ngôn ngữ sang mã chuẩn cho model
            var languageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "vi", "vi_VN" }, { "vietnamese", "vi_VN" },
                { "en", "en_XX" }, { "english", "en_XX" },
                { "fr", "fr_XX" }, { "french", "fr_XX" },
                { "de", "de_DE" }, { "german", "de_DE" },
                { "es", "es_XX" }, { "spanish", "es_XX" },
                { "ja", "ja_XX" }, { "japanese", "ja_XX" },
                { "ko", "ko_KR" }, { "korean", "ko_KR" },
                { "zh", "zh_CN" }, { "chinese", "zh_CN" },
                { "it", "it_IT" }, { "italian", "it_IT" },
                { "pt", "pt_XX" }, { "portuguese", "pt_XX" },
                { "ru", "ru_RU" }, { "russian", "ru_RU" },
                { "ar", "ar_AR" }, { "arabic", "ar_AR" },
                { "th", "th_TH" }, { "thai", "th_TH" },
                { "hi", "hi_IN" }, { "hindi", "hi_IN" }
            };

            // Lấy mã ngôn ngữ đích
            if (!languageMap.TryGetValue(targetLanguage, out var targetLangCode))
            {
                // Nếu không tìm thấy, thử dùng trực tiếp
                targetLangCode = targetLanguage.Length == 2 ? $"{targetLanguage}_XX" : targetLanguage;
            }

            // Model dịch - sử dụng mBART hoặc m2m100
            // mBART hỗ trợ 50 ngôn ngữ, m2m100 hỗ trợ 100 ngôn ngữ
            var translationModel = "facebook/mbart-large-50-many-to-many-mmt"; // hoặc "facebook/m2m100_418M"

            // Thử lại với retry mechanism
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Starting translation attempt {Attempt}/{MaxRetries} for text length: {Length}, target: {TargetLang}", 
                        attempt, MaxRetries, text.Length, targetLangCode);

                    // Chia nhỏ văn bản nếu quá dài (giới hạn ~512 tokens)
                    var chunks = SplitTextIntoChunks(text, 400);
                    var translatedChunks = new List<string>();

                    for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                    {
                        var chunk = chunks[chunkIndex];
                        var requestData = new
                        {
                            inputs = chunk,
                            parameters = new
                            {
                                src_lang = sourceLanguage ?? "auto",
                                tgt_lang = targetLangCode
                            }
                        };

                        var urls = new[]
                        {
                            $"https://api-inference.huggingface.co/models/{translationModel}",
                            $"https://router.huggingface.co/hf-inference/models/{translationModel}"
                        };

                        HttpResponseMessage? response = null;
                        string responseContent = "";
                        string? usedUrl = null;

                        bool chunkTranslated = false;
                        foreach (var url in urls)
                        {
                            try
                            {
                                _logger.LogInformation("Trying translation API endpoint: {Url} (chunk {ChunkIndex}/{TotalChunks})", 
                                    url, chunkIndex + 1, chunks.Count);
                                
                                response = await _httpClient.PostAsJsonAsync(url, requestData, _jsonOptions);
                                responseContent = await response.Content.ReadAsStringAsync();
                                usedUrl = url;
                                
                                if (response.IsSuccessStatusCode)
                                {
                                    chunkTranslated = true;
                                    _logger.LogInformation("Successfully translated chunk {ChunkIndex}/{TotalChunks} using endpoint: {Url}", 
                                        chunkIndex + 1, chunks.Count, url);
                                    break;
                                }
                                
                                // Log chi tiết hơn về lỗi
                                _logger.LogWarning("Endpoint {Url} returned status {StatusCode} for chunk {ChunkIndex}", 
                                    url, response.StatusCode, chunkIndex + 1);
                                
                                if (response.StatusCode == System.Net.HttpStatusCode.Gone || 
                                    response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    continue; // Thử endpoint tiếp theo
                                }
                                
                                // Các lỗi khác, không thử endpoint tiếp theo
                                break;
                            }
                            catch (HttpRequestException ex)
                            {
                                _logger.LogWarning("Failed to connect to {Url} for chunk {ChunkIndex}: {Error}", 
                                    url, chunkIndex + 1, ex.Message);
                                continue; // Thử endpoint tiếp theo
                            }
                            catch (TaskCanceledException ex)
                            {
                                _logger.LogWarning("Timeout connecting to {Url} for chunk {ChunkIndex}: {Error}", 
                                    url, chunkIndex + 1, ex.Message);
                                continue; // Thử endpoint tiếp theo
                            }
                        }

                        if (!chunkTranslated || response == null || !response.IsSuccessStatusCode)
                        {
                            // Nếu model không hỗ trợ, thử model khác (chỉ ở lần thử đầu tiên)
                            if (response?.StatusCode == System.Net.HttpStatusCode.NotFound && attempt == 1 && translationModel == "facebook/mbart-large-50-many-to-many-mmt")
                            {
                                _logger.LogInformation("Model {Model} not found, switching to alternative model", translationModel);
                                translationModel = "facebook/m2m100_418M";
                                // Break khỏi vòng lặp chunks và retry lại từ đầu với model mới
                                break;
                            }

                            _logger.LogWarning("Translation failed for chunk {ChunkIndex}/{TotalChunks}, status: {StatusCode}. Will retry in next attempt.", 
                                chunkIndex + 1, chunks.Count, response?.StatusCode);
                            
                            // Bỏ qua chunk này và tiếp tục với chunk tiếp theo
                            // Nếu tất cả chunks đều fail, sẽ retry lại ở attempt tiếp theo
                            continue;
                        }

                        // Parse response
                        try
                        {
                            var trimmedContent = responseContent.Trim();
                            if (trimmedContent.StartsWith("{") || trimmedContent.StartsWith("["))
                            {
                                var jsonNode = JsonNode.Parse(responseContent);
                                
                                if (jsonNode is JsonArray jsonArray && jsonArray.Count > 0)
                                {
                                    var firstItem = jsonArray[0];
                                    if (firstItem is JsonObject itemObj)
                                    {
                                        string? translatedText = null;
                                        
                                        if (itemObj.ContainsKey("translation_text"))
                                        {
                                            translatedText = itemObj["translation_text"]?.ToString();
                                        }
                                        else if (itemObj.ContainsKey("generated_text"))
                                        {
                                            translatedText = itemObj["generated_text"]?.ToString();
                                        }
                                        
                                        if (!string.IsNullOrWhiteSpace(translatedText))
                                        {
                                            translatedChunks.Add(translatedText);
                                            continue;
                                        }
                                    }
                                    else if (firstItem is JsonValue jsonValue)
                                    {
                                        var translatedText = jsonValue.ToString();
                                        if (!string.IsNullOrWhiteSpace(translatedText))
                                        {
                                            translatedChunks.Add(translatedText);
                                            continue;
                                        }
                                    }
                                }
                                else if (jsonNode is JsonObject jsonObj)
                                {
                                    if (jsonObj.ContainsKey("translation_text"))
                                    {
                                        var translatedText = jsonObj["translation_text"]?.ToString();
                                        if (!string.IsNullOrWhiteSpace(translatedText))
                                        {
                                            translatedChunks.Add(translatedText);
                                            continue;
                                        }
                                    }
                                    else if (jsonObj.ContainsKey("generated_text"))
                                    {
                                        var translatedText = jsonObj["generated_text"]?.ToString();
                                        if (!string.IsNullOrWhiteSpace(translatedText))
                                        {
                                            translatedChunks.Add(translatedText);
                                            continue;
                                        }
                                    }
                                }
                            }
                            
                            // Nếu không parse được JSON, có thể response là string trực tiếp
                            if (!string.IsNullOrWhiteSpace(responseContent))
                            {
                                translatedChunks.Add(responseContent.Trim());
                                continue;
                            }
                        }
                        catch (JsonException parseEx)
                        {
                            _logger.LogError(parseEx, "Failed to parse translation response");
                        }

                        // Nếu không dịch được chunk này, bỏ qua
                        _logger.LogWarning("Could not extract translation from response for chunk");
                    }

                    if (translatedChunks.Count > 0)
                    {
                        var result = string.Join(" ", translatedChunks);
                        _logger.LogInformation("Successfully translated {ChunksCount}/{TotalChunks} chunks, result length: {Length}", 
                            translatedChunks.Count, chunks.Count, result.Length);
                        return result;
                    }

                    // Nếu không dịch được chunk nào, retry lại
                    _logger.LogWarning("Failed to translate any chunks in attempt {Attempt}/{MaxRetries}. Translated: {TranslatedCount}/{TotalChunks}", 
                        attempt, MaxRetries, translatedChunks.Count, chunks.Count);
                    
                    if (attempt < MaxRetries)
                    {
                        _logger.LogInformation("Retrying translation after {Delay} seconds...", RetryDelaySeconds);
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during translation (attempt {Attempt}/{MaxRetries})", attempt, MaxRetries);
                    
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelaySeconds * 1000);
                        continue;
                    }
                    return "Đã xảy ra lỗi khi dịch văn bản: " + ex.Message;
                }
            }

            return "Không thể dịch văn bản sau nhiều lần thử.";
        }

        /// <summary>
        /// Test kết nối đến Hugging Face API
        /// </summary>
        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                var testText = "This is a test.";
                var url = $"https://api-inference.huggingface.co/models/{_model}";
                
                _logger.LogInformation("Testing HuggingFace API connection to: {Url}", url);
                
                var requestData = new
                {
                    inputs = testText,
                    parameters = new
                    {
                        max_length = 50,
                        min_length = 10
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(url, requestData, _jsonOptions);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation("Test response - Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, 
                    responseContent.Length > 500 ? responseContent.Substring(0, 500) : responseContent);

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Kết nối thành công!");
                }
                else
                {
                    return (false, $"Lỗi: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing HuggingFace API connection");
                return (false, $"Lỗi kết nối: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
