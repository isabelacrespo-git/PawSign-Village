using UnityEngine;

public class SuccessEffectsTrigger : MonoBehaviour
{
    [Tooltip("Optional parent containing particle systems to trigger.")]
    [SerializeField] private Transform effectsRoot;

    [Tooltip("Optional explicit list of particle systems to trigger.")]
    [SerializeField] private ParticleSystem[] effects;

    public void PlayEffects()
    {
        if (effects != null && effects.Length > 0)
        {
            for (int i = 0; i < effects.Length; i++)
                PlayParticle(effects[i]);
        }

        if (effectsRoot != null)
        {
            ParticleSystem[] rootEffects = effectsRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < rootEffects.Length; i++)
                PlayParticle(rootEffects[i]);
        }
    }

    private static void PlayParticle(ParticleSystem effect)
    {
        if (effect == null)
            return;

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.Play();
    }
}
