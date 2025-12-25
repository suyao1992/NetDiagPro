using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace NetDiagPro.Services;

/// <summary>
/// Zep AI 智能助手服务
/// </summary>
public class ZepAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl = "https://api.getzep.com/api/v2";
    private string? _currentUserId;
    private string? _currentThreadId;

    public ZepAIService(string apiKey)
    {
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Api-Key {apiKey}");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>
    /// 初始化用户
    /// </summary>
    public async Task<bool> InitializeUserAsync(string userId, string? firstName = null)
    {
        try
        {
            var user = new
            {
                user_id = userId,
                first_name = firstName ?? "User",
                metadata = new { app = "NetDiagPro", version = "4.0" }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/users", user);
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _currentUserId = userId;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 创建新的对话线程
    /// </summary>
    public async Task<string?> CreateThreadAsync()
    {
        if (string.IsNullOrEmpty(_currentUserId)) return null;

        try
        {
            var threadId = Guid.NewGuid().ToString("N");
            var thread = new
            {
                session_id = threadId,
                user_id = _currentUserId,
                metadata = new { created_at = DateTime.UtcNow.ToString("O") }
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/sessions", thread);
            
            if (response.IsSuccessStatusCode)
            {
                _currentThreadId = threadId;
                return threadId;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 添加消息到线程
    /// </summary>
    public async Task<bool> AddMessageAsync(string role, string content, string? name = null)
    {
        if (string.IsNullOrEmpty(_currentThreadId)) return false;

        try
        {
            var messages = new[]
            {
                new
                {
                    role = role,
                    content = content,
                    role_type = role == "user" ? "user" : "assistant",
                    metadata = new { timestamp = DateTime.UtcNow.ToString("O") }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/sessions/{_currentThreadId}/messages", 
                new { messages });
            
            return response.IsSuccessStatusCode;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 记录网络事件到知识图谱
    /// </summary>
    public async Task<bool> RecordNetworkEventAsync(NetworkEventData eventData)
    {
        if (string.IsNullOrEmpty(_currentUserId)) return false;

        try
        {
            var data = JsonSerializer.Serialize(eventData);
            var payload = new
            {
                user_id = _currentUserId,
                type = "json",
                data = data
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/graph", payload);
            return response.IsSuccessStatusCode;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 获取用户上下文
    /// </summary>
    public async Task<string?> GetUserContextAsync()
    {
        if (string.IsNullOrEmpty(_currentThreadId)) return null;

        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/sessions/{_currentThreadId}/memory");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 搜索知识图谱
    /// </summary>
    public async Task<string?> SearchGraphAsync(string query)
    {
        if (string.IsNullOrEmpty(_currentUserId)) return null;

        try
        {
            var payload = new
            {
                user_id = _currentUserId,
                query = query,
                limit = 10
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/graph/search", payload);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 获取当前用户ID
    /// </summary>
    public string? CurrentUserId => _currentUserId;

    /// <summary>
    /// 获取当前线程ID
    /// </summary>
    public string? CurrentThreadId => _currentThreadId;

    /// <summary>
    /// 检查服务是否已初始化
    /// </summary>
    public bool IsInitialized => !string.IsNullOrEmpty(_currentUserId) && !string.IsNullOrEmpty(_currentThreadId);
}

/// <summary>
/// 网络事件数据
/// </summary>
public class NetworkEventData
{
    public string EventType { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double? DownloadMbps { get; set; }
    public double? UploadMbps { get; set; }
    public double? PingMs { get; set; }
    public string? Issue { get; set; }
    public string? Resolution { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}
