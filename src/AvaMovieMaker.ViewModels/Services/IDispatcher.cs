namespace AvaMovieMaker.ViewModels.Services;

public interface IDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
