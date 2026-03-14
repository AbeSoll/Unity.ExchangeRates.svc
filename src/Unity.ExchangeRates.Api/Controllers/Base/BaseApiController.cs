using FluentResults;
using Mediator;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.ExchangeRates.Service.Common.Errors;
using Unity.ExchangeRates.Api.ViewModels.Response;
using static Unity.ExchangeRates.Service.Models.Constants.CommonConstants;

namespace Unity.ExchangeRates.Api.Controllers.Base
{
    public class BaseApiController : ControllerBase
    {
        private IMediator _mediator;
        protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>();

        protected new IActionResult ApiResponse<T>(
            Result<T> result,
            [CallerMemberName] string? caller = default)
        {
            if (result is null || (result.IsSuccess && result.ValueOrDefault is null))
                return HandleNullProblem();

            if (result.IsSuccess)
                return Ok(result.ValueOrDefault);

            if (result.Errors.Any(x => x is ValidationError))
                return HandleValidationProblem(result);

            return HandleFluentResultProblem(result);
        }

        protected new IActionResult ApiResponse<T, T2>(
            Result<T> responseResult,
            Result<T2> result,
            [CallerMemberName] string? caller = default)
        {
            if (result is null || (result.IsSuccess && result.ValueOrDefault is null))
                return HandleNullProblem();

            if (result.IsSuccess)
                return Ok(responseResult.ValueOrDefault);

            if (result.Errors.Any(x => x is ValidationError))
                return HandleValidationProblem(result);

            return HandleFluentResultProblem(result);
        }

        protected IActionResult UnhandledProblem()
        {
            int statusCode = StatusCodes.Status500InternalServerError;

            Response.StatusCode = statusCode;
            var resp = new BaseResponse()
            {
                status = StandardFormat.FailedStatus,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture),
                traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                errorCode = "00500",
                errorMsg = ResponseMessage.GENERAL_ERROR
            };
            return new JsonResult(resp);
        }

        private IActionResult HandleNullProblem()
        {
            int statusCode = StatusCodes.Status404NotFound;

            Response.StatusCode = statusCode;
            var resp = new BaseResponse()
            {
                status = StandardFormat.FailedStatus,
                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffzzz", System.Globalization.CultureInfo.InvariantCulture),
                traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                errorCode = "00404",
                errorMsg = String.Empty
            };
            return new JsonResult(resp);
        }

        private IActionResult HandleValidationProblem<T>(Result<T> result)
        {
            IError? firstError = result.Errors.FirstOrDefault();

            if (firstError is null)
                return UnhandledProblem();

            Response.StatusCode = StatusCodes.Status400BadRequest;
            var resp = JsonConvert.DeserializeObject<BaseResponse>(JsonConvert.SerializeObject(firstError));
            return new JsonResult(resp);
        }

        private IActionResult HandleFluentResultProblem<T>(Result<T> result)
        {
            IError? firstError = result.Errors.FirstOrDefault();

            if (firstError is null)
                return UnhandledProblem();

            int statusCode = firstError switch
            {
                NotFoundError => StatusCodes.Status404NotFound,
                GeneralError => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status500InternalServerError
            };

            Response.StatusCode = statusCode;
            var resp = JsonConvert.DeserializeObject<BaseResponse>(JsonConvert.SerializeObject(firstError));
            return new JsonResult(resp);
        }
    }
}
