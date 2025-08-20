using DATN1API.Pay;
using DATN1API.Services;
using Microsoft.AspNetCore.Mvc;

public class PaymentController : Controller
{
    private readonly IVnPayService _vnPayService;

    public PaymentController(IVnPayService vnPayService)
    {
        _vnPayService = vnPayService;
    }

    // Tạo URL thanh toán và chuyển hướng tới VNPAY
    public IActionResult CreatePaymentUrlVnpay(PaymentInformationModel model)
    {
        var url = _vnPayService.CreatePaymentUrl(model, HttpContext);
        return Redirect(url);
    }

    // Callback từ VNPAY sau khi thanh toán
    [HttpGet]
    public IActionResult PaymentCallbackVnpay()
    {
        var response = _vnPayService.PaymentExecute(Request.Query);
        return Json(response);
    }
}
