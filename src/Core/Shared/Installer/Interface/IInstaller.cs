namespace Shared.Installer.Interface;

public interface IInstaller
{
    void Install(string[] args);
    void Uninstall();
}