using Platformer.Core;
using Platformer.Mechanics;
using static Platformer.Core.Simulation;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player health reaches 0. This usually would result in a 
    /// PlayerDeath event.
    /// </summary>
    /// <typeparam name="HealthIsZero"></typeparam>
    public class HealthIsZero : Simulation.Event<HealthIsZero>
    {
        public Health health;

        public override void Execute()
        {
            // Only the legacy platformer player uses the PlayerDeath/PlayerSpawn event flow.
            // MirrorTrial actors handle death and respawning in their own controller.
            if (health && health.GetComponent<PlayerController>())
                Schedule<PlayerDeath>();
        }
    }
}