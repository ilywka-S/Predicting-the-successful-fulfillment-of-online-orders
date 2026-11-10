namespace OrderSense.Client.Core.ViewModels;

internal static class ApiErrorMessages
{
    public static string For(Exception exception, string fallback) => exception switch
    {
        ApiException { StatusCode: 401 or 403 } => "Немає доступу. Увійдіть знову.",
        ApiException { StatusCode: 503 } => "Сервіс прогнозування тимчасово недоступний. Спробуйте за хвилину.",
        ApiException { StatusCode: >= 500 } => "Сервер тимчасово недоступний. Спробуйте пізніше.",
        HttpRequestException => "Немає підключення до сервера. Перевірте мережу.",
        OperationCanceledException => "Сервер не відповідає. Спробуйте ще раз.",
        _ => fallback,
    };
}
