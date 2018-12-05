$(document).ready(function()
{
    // Run SignalR to accept events from the backend
    NProgress.start();
    var signalrHub = new signalR.HubConnectionBuilder().withUrl("/ws-updater").build();
    signalrHub.on("AnotherUselessAction", function () {
        alert("ok");
    });
    signalrHub.start();

    // Check if the federation is enabled, if it's not the case a modal is displayed to enabled it
    $.get("/check-federation", function(response)
    {
        if(response == false)
        {
            $("#enableF").modal("show");
        }
    });

    /*$(".loader").fadeOut(function()
    {
        $("#loading-content").fadeIn();
    });*/

    NProgress.done();
});

function DisplayNotification(text)
{
    setTimeout(function()
    {
        Snackbar.show({text: text, pos: "bottom-center"});
    }, 1000);
}

function BeginAction()
{
    NProgress.start();
}
function CompleteAction()
{
    NProgress.done();
}

function HideModals()
{
    $(".modal").modal("hide");
}

/* STRATIS MAINNET ACTIONS EVENT */
function StratisNodeStopped()
{
    DisplayNotification("Mainnet node sucessfully stopped.");
}
function StratisNodeStopFailed()
{
    DisplayNotification("Mainnet node cannot be stopped.");
}

function StratisCrosschainResynced()
{
    DisplayNotification("Mainnet crosschain Sucessfully resynced.");
}
function StratisCrosschainResyncFailed()
{
    DisplayNotification("Unable to resynced Mainnet crosschain .");
}

function StratisResyncedBlockchain()
{
    DisplayNotification("Mainnet blockchain sucessfully resynced.");
}
function StratisResyncBlockchainFailed()
{
    DisplayNotification("Unable to resync the blockchain.");
}

/* SIDECHAIN ACTIONS EVENT */
function SidechainNodeStopped()
{
    DisplayNotification("Mainnet node sucessfully stopped.");
}
function SidechainNodeStopFailed()
{
    DisplayNotification("Mainnet node cannot be stopped.");
}

function SidechainCrosschainResynced()
{
    DisplayNotification("Mainnet crosschain Sucessfully resynced.");
}
function SidechainCrosschainResyncFailed()
{
    DisplayNotification("Unable to resynced Mainnet crosschain .");
}

function SidechainResyncedBlockchain()
{
    DisplayNotification("Mainnet blockchain sucessfully resynced.");
}
function SidechainResyncBlockchainFailed()
{
    DisplayNotification("Unable to resync the blockchain.");
}