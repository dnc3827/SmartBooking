using System;
using System.Collections.Generic;

namespace SmartBookingAPI.Models;

public partial class Customer
{
    public int CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public string? Username { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "Customer"; // Mặc định ai tạo tài khoản cũng là Customer

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
