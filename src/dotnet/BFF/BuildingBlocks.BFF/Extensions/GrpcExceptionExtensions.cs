using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace BuildingBlocks.BFF.Extensions;

public static class GrpcExceptionExtensions
{
    public static IActionResult ToActionResult(this RpcException ex)
    {
        return ex.StatusCode switch
        {
            StatusCode.InvalidArgument => new BadRequestObjectResult(new { detail = ex.Status.Detail }),
            StatusCode.Unauthenticated => new UnauthorizedObjectResult(new { detail = ex.Status.Detail }),
            StatusCode.PermissionDenied => new ObjectResult(new { detail = ex.Status.Detail }) { StatusCode = 403 },
            StatusCode.NotFound => new NotFoundObjectResult(new { detail = ex.Status.Detail }),
            StatusCode.AlreadyExists => new ConflictObjectResult(new { detail = ex.Status.Detail }),
            StatusCode.FailedPrecondition => new ObjectResult(new { detail = ex.Status.Detail }) { StatusCode = 422 },
            StatusCode.ResourceExhausted => new ObjectResult(new { detail = ex.Status.Detail }) { StatusCode = 429 },
            StatusCode.DeadlineExceeded => new ObjectResult(new { detail = "The request timed out." }) { StatusCode = 504 },
            StatusCode.Unavailable => new ObjectResult(new { detail = "Service is temporarily unavailable." }) { StatusCode = 503 },
            _ => new ObjectResult(new { detail = "An internal server error occurred." }) { StatusCode = 500 }
        };
    }
}
