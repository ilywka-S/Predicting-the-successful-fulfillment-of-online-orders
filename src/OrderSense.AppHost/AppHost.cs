var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddConnectionString("ordersense");

var jwtKey = builder.AddParameter("jwt-key", secret: true);

var adminEmail = builder.AddParameter("admin-email");
var adminPassword = builder.AddParameter("admin-password", secret: true);

var prediction = builder.AddProject<Projects.OrderSense_PredictionService>("prediction");

builder.AddProject<Projects.OrderSense_Api>("api")
    .WithReference(db)
    .WithReference(prediction)
    .WithEnvironment("Jwt__Key", jwtKey)
    .WithEnvironment("Seed__AdminEmail", adminEmail)
    .WithEnvironment("Seed__AdminPassword", adminPassword);

builder.Build().Run();