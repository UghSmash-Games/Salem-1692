using UnityEngine;
using UnityEngine.Audio;

public class DeckShuffleAnimation : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private float timeCounter = 0;
    public float shuffleSoundTimer = 0.5f;
    private AudioSource shuffleSoundAudio;
    private bool isShuffling = true;
    private Animator animator;

    private void Awake()
    {
        shuffleSoundAudio = GetComponent<AudioSource>();
        if (shuffleSoundAudio == null)
        {
            Debug.LogError("DeckShuffleAnimation requires an AudioSource component for shuffle sound.");
        }
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("DeckShuffleAnimation requires an Animator component for shuffle animation.");
        }
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        timeCounter += Time.deltaTime;
        if (timeCounter >= shuffleSoundTimer && isShuffling)
        {
            timeCounter = 0f;
            LoopShuffleSound();
        }
    }

    void LoopShuffleSound()
    {
        shuffleSoundAudio.Stop();
        shuffleSoundAudio.time = 0f;
        shuffleSoundAudio.Play();
    }

    public void StopShuffleAnimation()
    {
        shuffleSoundAudio.Stop();
        timeCounter = 0f;
        isShuffling = false;
        animator.SetBool("isShuffling", false);
    }
}
