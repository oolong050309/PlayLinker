namespace PlayLinker.Models;

/// <summary>
/// 统一API响应格式
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 响应代码
    /// </summary>
    public string Code { get; set; } = "OK";

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = "操作成功";

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    public ResponseMeta Meta { get; set; } = new();

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string message = "操作成功")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Code = "OK",
            Message = message,
            Data = data,
            Meta = new ResponseMeta
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Version = "1.0"
            }
        };
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    public static ApiResponse<T> ErrorResponse(string code, string message, T? data = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Code = code,
            Message = message,
            Data = data,
            Meta = new ResponseMeta
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Version = "1.0"
            }
        };
    }
}

/// <summary>
/// 响应元数据
/// </summary>
public class ResponseMeta
{
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string Version { get; set; } = "1.0";
}

/// <summary>
/// 分页元数据
/// </summary>
public class PaginationMeta
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

