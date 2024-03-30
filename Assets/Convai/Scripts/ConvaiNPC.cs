using System;
using System.Collections;
using Convai.gRPCAPI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Grpc.Core;
using Service;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class ConvaiNPC : MonoBehaviour
{
    [HideInInspector]
    public List<GetResponseResponse> getResponseResponses = new List<GetResponseResponse>();

    public string sessionID = "-1";

    [HideInInspector]
    public string stringCharacterText = "";

    List<ResponseAudio> ResponseAudios = new List<ResponseAudio>();

    public class ResponseAudio
    {
        public AudioClip audioClip;
        public string audioTranscript;
    };

    ActionConfig actionConfig = new ActionConfig();

    [SerializeField] public string CharacterID;

    private AudioSource audioSource;
    private Animator characterAnimator;

    [SerializeField] private TextMeshProUGUI CharacterText;

    bool animationPlaying = false;

    bool playingStopLoop = false;

    [Serializable]
    public class Character
    {
        [SerializeField] public string Name;
        [SerializeField] public string Bio;
    };

    [Serializable]
    public class Object
    {
        [SerializeField] public string Name;
        [SerializeField] public string Description;
    }

    [SerializeField] bool isActionActive;

    [Serializable]
    public class CharacterActionConfig
    {
        [SerializeField] public Character[] Characters;
        [SerializeField] public Object[] Objects;
        [SerializeField] public string[] stringActions;
    }

    [SerializeField] private CharacterActionConfig characterActionConfig;

    private Channel channel;
    private ConvaiService.ConvaiServiceClient client;

    private const int AUDIO_SAMPLE_RATE = 44100;
    private const string GRPC_API_ENDPOINT = "stream.convai.com";

    private int recordingFrequency = AUDIO_SAMPLE_RATE;
    private int recordingLength = 30;

    private ConvaiGRPCAPI grpcAPI;

    [SerializeField] public bool isCharacterActive;
    [SerializeField] bool enableTestMode;
    [SerializeField] string testUserQuery;

    [SerializeField] private Button talkButton;  // UI button for talking
    [SerializeField] private Button goButton;    // UI button for stopping recording and getting character's response

    private void Awake()
    {
        grpcAPI = FindObjectOfType<ConvaiGRPCAPI>();
        audioSource = GetComponent<AudioSource>();
        characterAnimator = GetComponent<Animator>();
    }

    private void Start()
    {
        StartCoroutine(playAudioInOrder());

        // Add a listener to the talk button to handle talk action
        talkButton.onClick.AddListener(StartTalk);

        // Add a listener to the "Go" button to handle stopping recording and getting character's response
        goButton.onClick.AddListener(StopRecordingAndShowResponse);

        #region GRPC_SETUP
        SslCredentials credentials = new SslCredentials();

        channel = new Channel(GRPC_API_ENDPOINT, credentials);

        client = new ConvaiService.ConvaiServiceClient(channel);
        #endregion

        #region ACTIONS_SETUP
        foreach (string action in characterActionConfig.stringActions)
        {
            actionConfig.Actions.Add(action);
        }

        foreach (Character character in characterActionConfig.Characters)
        {
            ActionConfig.Types.Character rpcCharacter = new ActionConfig.Types.Character
            {
                Name = character.Name,
                Bio = character.Bio
            };
        }

        foreach (Object eachObject in characterActionConfig.Objects)
        {
            ActionConfig.Types.Object rpcObject = new ActionConfig.Types.Object
            {
                Name = eachObject.Name,
                Description = eachObject.Description
            };
            actionConfig.Objects.Add(rpcObject);
        }
        #endregion
    }

    private void StartTalk()
    {
        grpcAPI.StartRecordAudio(client, isActionActive, recordingFrequency, recordingLength, CharacterID, actionConfig, enableTestMode, testUserQuery);
    }

    private void StopRecordingAndShowResponse()
    {
        grpcAPI.StopRecordAudio();
    }

    private void Update()
    {
        if (isCharacterActive)
        {
            CharacterText.text = stringCharacterText;

            if (Input.GetKeyDown(KeyCode.LeftControl))
            {
                StartTalk(); // Start recording when LeftControl is pressed
            }

            if (Input.GetKeyUp(KeyCode.LeftControl))
            {
                StopRecordingAndShowResponse(); // Stop recording when LeftControl is released
            }
        }

        if (Input.GetKey(KeyCode.R) && Input.GetKey(KeyCode.Equals))
        {
            SceneManager.LoadScene(0);
        }

        if (Input.GetKey(KeyCode.Escape) && Input.GetKey(KeyCode.Equals))
        {
            Application.Quit();
        }

        if (getResponseResponses.Count > 0)
        {
            ProcessResponseAudio(getResponseResponses[0]);
            getResponseResponses.Remove(getResponseResponses[0]);
        }

        if (ResponseAudios.Count > 0)
        {
            if (animationPlaying == false)
            {
                animationPlaying = true;
                characterAnimator.SetBool("Talk", true);
            }
        }
        else
        {
            if (animationPlaying == true)
            {
                animationPlaying = false;
                characterAnimator.SetBool("Talk", false);
            }
        }
    }

    void ProcessResponseAudio(GetResponseResponse getResponseResponse)
    {
        if (isCharacterActive)
        {
            string tempString = "";

            if (getResponseResponse.AudioResponse.TextData != null)
                tempString = getResponseResponse.AudioResponse.TextData;

            byte[] byteAudio = getResponseResponse.AudioResponse.AudioData.ToByteArray();

            AudioClip clip = grpcAPI.ProcessByteAudioDataToAudioClip(byteAudio, getResponseResponse.AudioResponse.AudioConfig.SampleRateHertz.ToString());

            ResponseAudios.Add(new ResponseAudio
            {
                audioClip = clip,
                audioTranscript = tempString
            });
        }
    }

    IEnumerator playAudioInOrder()
    {
        while (!playingStopLoop)
        {
            if (ResponseAudios.Count > 0)
            {
                audioSource.PlayOneShot(ResponseAudios[0].audioClip);
                stringCharacterText = ResponseAudios[0].audioTranscript;
                yield return new WaitForSeconds(ResponseAudios[0].audioClip.length);
                ResponseAudios.Remove(ResponseAudios[0]);
            }
            else
                yield return null;
        }
    }

    void OnApplicationQuit()
    {
        playingStopLoop = true;
    }
}


