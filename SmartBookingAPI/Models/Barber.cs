using System;
using System.Collections.Generic;

namespace SmartBookingAPI.Models;

public partial class Barber
{
    public int BarberId { get; set; }

    public string BarberName { get; set; } = null!;

    public double? Rating { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
