using Bang;
using Bang.Entities;
using Bang.Interactions;
using Murder.Components;

namespace Murder.Interactions;

public readonly struct FadeSpriteInteraction() : IInteraction
{
    public readonly float FadeDuration = 1f;
    public readonly float StartAlpha = 1;
    public readonly float EndAlpha = 0;
    public readonly bool DestroyOnEnd = true;

    public void Interact(World world, Entity interactor, Entity? interacted)
    {
        if (interacted is not Entity entity)
        {
            return;
        }

        FadeSpriteFlags flags = FadeSpriteFlags.Alpha;
        if (DestroyOnEnd)
        {
            flags |= FadeSpriteFlags.DestroyOnEnd;
        }

        entity.SetFadeSprite(Game.Now, Game.Now + FadeDuration, StartAlpha, EndAlpha, flags);
    }
}
