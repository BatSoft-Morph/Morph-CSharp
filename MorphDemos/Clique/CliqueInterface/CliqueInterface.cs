namespace Clique.Interface
{
    public static class CliqueInterface
    {
        public const string ServiceName = "Morph.Demo.Clique";

        public const string ConnectorTypeName = "CliqueConnector";
        public const string DiplomatTypeName = "CliqueDiplomat";

        public static CliqueInstanceFactories Factories = new CliqueInstanceFactories();
    }

    public interface ICliqueConnector
    {
        ICliqueDiplomat Hello(ICliqueDiplomat newFriend);
    }

    public interface ICliqueDiplomat
    {
        string Text { get; }

        void ChangeText(ICliqueDiplomat friend, string text);

        void Bye(ICliqueDiplomat friend);
    }
}