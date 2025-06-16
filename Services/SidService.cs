using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

public class SidPlayerService
{
    private readonly IJSRuntime _js;

    public SidPlayerService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task Init(int bufferLen = 16384, double backgroundNoise = 0.0005)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.init", bufferLen, backgroundNoise);
    }

    public async Task LoadStart(string sidUrl, int subt)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.loadstart", sidUrl, subt);
    }

    public async Task LoadInit(string sidUrl, int subt)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.loadinit", sidUrl, subt);
    }

    public async Task Start(int subt)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.start", subt);
    }

    public async Task PlayCont()
    {
        await _js.InvokeVoidAsync("window.sidPlayer.playcont");
    }

    public async Task Pause()
    {
        await _js.InvokeVoidAsync("window.sidPlayer.pause");
    }

    public async Task Stop()
    {
        await _js.InvokeVoidAsync("window.sidPlayer.stop");
    }

    public async Task<string> GetTitle()
    {
        return await _js.InvokeAsync<string>("window.sidPlayer.gettitle");
    }

    public async Task<string> GetAuthor()
    {
        return await _js.InvokeAsync<string>("window.sidPlayer.getauthor");
    }

    public async Task<string> GetInfo()
    {
        return await _js.InvokeAsync<string>("window.sidPlayer.getinfo");
    }

    public async Task<int> GetSubtunes()
    {
        return await _js.InvokeAsync<int>("window.sidPlayer.getsubtunes");
    }

    public async Task<int> GetPrefModel()
    {
        return await _js.InvokeAsync<int>("window.sidPlayer.getprefmodel");
    }

    public async Task<int> GetModel()
    {
        return await _js.InvokeAsync<int>("window.sidPlayer.getmodel");
    }

    public async Task<double> GetOutput()
    {
        return await _js.InvokeAsync<double>("window.sidPlayer.getoutput");
    }

    public async Task<int> GetPlayTime()
    {
        return await _js.InvokeAsync<int>("window.sidPlayer.getplaytime");
    }

    public async Task SetModel(int model)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.setmodel", model);
    }

    public async Task SetVolume(double vol)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.setvolume", vol);
    }

    public async Task SetLoadCallback(string callbackName)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.setloadcallback", callbackName);
    }

    public async Task SetStartCallback(string callbackName)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.setstartcallback", callbackName);
    }

    public async Task SetEndCallback(string callbackName, int seconds)
    {
        await _js.InvokeVoidAsync("window.sidPlayer.setendcallback", callbackName, seconds);
    }
}
