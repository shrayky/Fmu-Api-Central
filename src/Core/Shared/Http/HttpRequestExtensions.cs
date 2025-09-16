using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Shared.Http
{
    public static class HttpRequestExtensions
    {
        public static async Task<Result<T, string>> ReadJsonFromBody<T>(this HttpRequest request, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (request.Body == null)
                return Result.Failure<T, string>("Пустое тело запроса");

            try
            {
                var value = await JsonSerializer.DeserializeAsync<T>(request.Body, options ?? new JsonSerializerOptions(), cancellationToken);
                if (value == null)
                    return Result.Failure<T, string>($"Не удалось привести тело запроса к типу {typeof(T).Name}");

                return Result.Success<T, string>(value);
            }
            catch (Exception ex)
            {
                return Result.Failure<T, string>($"Ошибка десериализации: {ex.Message}");
            }
        }
    }
}
