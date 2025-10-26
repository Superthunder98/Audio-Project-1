using UnityEngine;

public class AudioTrigger : MonoBehaviour
{
   
        AudioSource audioSource;




        void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }
        public void OnTriggerEnter(Collider other)
        {
            if (audioSource != null)
            {
                if (other.transform.tag == "Player")
                {
                    audioSource.Play();
                }
            }
        
        }
}
