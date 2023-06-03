namespace GCodeCorrector.Infrastructure
{
    public class MenuWithSubItems
    {
        public string Caption { get; }
        public string[] MenuItems { get; }

        public MenuWithSubItems(string caption, string[] menuItems)
        {
            Caption = caption;
            MenuItems = menuItems;
        }
    }
}