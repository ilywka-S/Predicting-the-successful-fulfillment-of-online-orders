var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddConnectionString("ordersense");

var prediction = builder.AddProject<Projects.OrderSense_PredictionService>("prediction");

builder.AddProject<Projects.OrderSense_Api>("api")
    .WithReference(db)
    .WithReference(prediction);

builder.Build().Run();