var builder = DistributedApplication.CreateBuilder(args);

var catalogApi = builder.AddProject<Projects.CatalogApi>("catalogapi");
//.WithHttpEndpoint(name: "http");

var ordersApi = builder.AddProject<Projects.OrdersApi>("ordersapi");
//.WithHttpEndpoint(name: "http");

builder.AddProject<Projects.BffGateway>("bffgateway")
    //.WithHttpEndpoint(name: "http")
    .WithEnvironment("Services__CatalogApi__BaseUrl", catalogApi.GetEndpoint("http"))
    .WithEnvironment("Services__OrdersApi__BaseUrl", ordersApi.GetEndpoint("http"))
    .WithReference(catalogApi)
    .WithReference(ordersApi)
    .WithExternalHttpEndpoints();

builder.Build().Run();
