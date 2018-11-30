$(document).ready(function()
{
    var signalrHub = new signalR.HubConnectionBuilder().withUrl("/ws-updater").build();
    signalrHub.on("ReceiveMessage", function (user, message) {
        alert("ok");
    });
    signalrHub.start();
});