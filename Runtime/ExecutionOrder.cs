namespace Ludo.UnityInject
{
    public static class ExecutionOrder
    {
        public const int SceneContext = -9000;
        public const int GameObjectContext = -8900; // Run early, but after SceneContext
    }
}