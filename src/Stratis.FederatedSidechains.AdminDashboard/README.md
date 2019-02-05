# How to Deploy Stratis.FederatedSidechains.AdminDashboard

The dashboard assets are managed with [Libman](https://docs.microsoft.com/en-us/aspnet/core/client-side/libman/libman-vs?view=aspnetcore-2.2) and are automatically restored when you build the project.

Before running the Dashboard in a Production environment you need to build minified assets with the `dotnet bundle` command.
