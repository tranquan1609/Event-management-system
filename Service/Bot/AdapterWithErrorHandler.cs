using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Connector.Authentication;

namespace DACSWEBSK.Service.Bot
{
    public class AdapterWithErrorHandler : CloudAdapter
    {
        public AdapterWithErrorHandler(
            IConfiguration configuration,
            ILogger<IBotFrameworkHttpAdapter> logger)
            : base(configuration, null, logger)
        {
            OnTurnError = async (turnContext, exception) =>
            {
                logger.LogError(exception, $"[OnTurnError] unhandled error : {exception.Message}");

                await turnContext.SendActivityAsync("Xin lỗi, đã xảy ra lỗi. Vui lòng thử lại sau.");
                await turnContext.TraceActivityAsync("OnTurnError Trace", exception.ToString(), "https://www.botframework.com/schemas/error", "TurnError");
            };
        }
    }
} 