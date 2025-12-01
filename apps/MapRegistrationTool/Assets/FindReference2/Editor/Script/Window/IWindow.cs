namespace vietlabs.fr2
{
    public interface IWindow
    {
        bool WillRepaint { get; set; }
        void Repaint();
    }
}
