using System;
using System.Collections.Generic;

namespace DATN1API.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int? OrderId { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? BankTransactionCode { get; set; }

    public string? PaymentContent { get; set; }

    public string? PaymentStatus { get; set; }

    public string? PaymentType { get; set; }

    public string? MethodName { get; set; }  // Tên phương thức thanh toán

    public bool IsActive { get; set; }  // Trạng thái kích hoạt

    public virtual Order? Order { get; set; }

}
