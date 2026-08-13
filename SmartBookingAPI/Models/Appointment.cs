using System;
using System.Collections.Generic;

namespace SmartBookingAPI.Models;

public partial class Appointment
{
    public int AppointmentId { get; set; }

    public int CustomerId { get; set; }

    public int BarberId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Barber Barber { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<Service> Services { get; set; } = new List<Service>();
}
