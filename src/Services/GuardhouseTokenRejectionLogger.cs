namespace Guardhouse.SDK.Services;

using Extensions;
using Microsoft.Extensions.Logging;

internal static class GuardhouseTokenRejectionLogger
{
    internal static void Log(ILogger logger, bool enableDebug, string? failureReason)
    {
        if (GuardhouseIntrospectionLogic.IsRoutineTokenRejection(failureReason))
        {
            logger.LogDebugIf(enableDebug, "Token rejected: {Reason}", failureReason);
            return;
        }

        logger.LogWarning("Token rejected: {Reason}", failureReason);
    }
}
