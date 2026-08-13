using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartBookingAPI.Models;
using SmartBookingAPI.Services;
using System.Text;
using Serilog;
using SmartBookingAPI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SmartBookingAPI.Services.Strategies;
using Microsoft.Extensions.Options;
using System.Reflection;


// ==========================================
// CẤU HÌNH SERILOG (Sổ khám bệnh)
// ==========================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Hệ thống SmartBooking đang khởi động...");

    // ==========================================
    // GIAI ĐOẠN 1: DÀN TRẬN (Cấu hình Services)
    // ==========================================
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddScoped<IPromotionService, PromotionService>();
    builder.Services.AddScoped<IPaymentGateway, PaymentService>();

    // ĐĂNG KÝ STRATEGY VÀO HỆ THỐNG
    builder.Services.AddScoped<IPromotionStrategy, PercentageDiscountStrategy>();
    builder.Services.AddScoped<IPromotionStrategy, FixedAmountDiscountStrategy>();

    builder.Services.AddScoped<IBarberService, BarberService>();
    builder.Services.AddScoped<IServiceService, ServiceService>();


    // Kích hoạt Serilog thay cho Log mặc định
    builder.Host.UseSerilog();
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = false;
        options.ValidateOnBuild = false;
    });

    // 1. Kết nối Database
    builder.Services.AddDbContext<SmartBookingDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 2. Đăng ký Service vào hệ thống (Dependency Injection)
    builder.Services.AddScoped<IAppointmentService, AppointmentService>();

    // 3. LẤY SECRET KEY TỪ APPSETTINGS.JSON
    var secretKey = builder.Configuration["JwtConfig:SecretKey"];
    if (string.IsNullOrEmpty(secretKey))
    {
        throw new Exception("Thảm họa: Chưa cấu hình JwtConfig:SecretKey trong file appsettings.json!");
    }
    var key = Encoding.UTF8.GetBytes(secretKey);

    builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(x =>
    {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = "HeThongDatLich", // Đảm bảo khớp với nơi phát hành token
            ValidateAudience = false,
            ValidAudience = "KháchHang",
            ClockSkew = TimeSpan.Zero
        };
    });

    // 4. Khai báo AUTHORIZATION (Phân quyền)
    builder.Services.AddAuthorization();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Bỏ qua lỗi vòng lặp khi serialize JSON
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

    // Khởi tạo PayOS từ appsettings.json
    string clientId = builder.Configuration["PayOS:ClientId"] ?? throw new Exception("Thiếu ClientId");
    string apiKey = builder.Configuration["PayOS:ApiKey"] ?? throw new Exception("Thiếu ApiKey");
    string checksumKey = builder.Configuration["PayOS:ChecksumKey"] ?? throw new Exception("Thiếu ChecksumKey");

    // Sửa thành Net.payOS.PayOS chuẩn chỉ để tránh đụng hàng tên gọi
    var payOsClient = new PayOS.PayOSClient(clientId, apiKey, checksumKey);
    builder.Services.AddSingleton(payOsClient); // Bơm đúng tên biến payOsClient vào hệ thống DI

    builder.Services.AddEndpointsApiExplorer();

    // 5. Cấu hình SWAGGER (Tạo ổ khóa JWT trên UI)
    builder.Services.AddSwaggerGen(opt =>
    {
        opt.SwaggerDoc("v1", new OpenApiInfo { Title = "SmartBooking API", Version = "v1" });
        opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Hãy dán Token vào đây (Gõ 'Bearer ' + dấu cách + Token)",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            BearerFormat = "JWT",
            Scheme = "Bearer"
        });
        opt.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type=ReferenceType.SecurityScheme, Id="Bearer" }
                },
                new string[]{}
            }
        });
        var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
        opt.IncludeXmlComments(xmlPath);
    });

    // ==========================================
    // GIAI ĐOẠN 2: XÂY DỰNG PIPELINE (Middleware)
    // ==========================================
    var app = builder.Build();

    // 6. TRẠM Y TẾ (Luôn đặt trên cùng để hứng mọi lỗi)
    app.UseMiddleware<SmartBookingAPI.Middlewares.GlobalExceptionMiddleware>();

    // 7. Các Middleware mặc định
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // 8. Trạm bảo vệ (Thứ tự bắt buộc: Authen trước, Author sau)
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // MỞ CỬA ĐÓN KHÁCH!
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Máy chủ khởi động thất bại hoặc bị sập đột ngột!");
}
finally
{
    Log.CloseAndFlush(); // Đóng sổ khi tắt App
}