using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class LogManager : MonoBehaviour
{
    public static LogManager Instance { get; private set; } // Singleton instance

    [SerializeField] private TextMeshProUGUI logText; 
    [SerializeField] private ScrollRect scrollRect; 
    [SerializeField] private RectTransform contentRect; 

    private const int maxLogLines = 100; 
    private readonly Queue<string> logQueue = new Queue<string>(); 

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Subscribe to Unity's log system
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        AddLog(logString);
    }

    public void AddLog(string message)
    {
        logQueue.Enqueue(message);

        if (logQueue.Count > maxLogLines)
        {
            logQueue.Dequeue();
        }

        logText.text = string.Join("\n", logQueue);

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        StartCoroutine(ScrollToBottom());
    }


    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame(); // Wait for UI update
        yield return new WaitForEndOfFrame(); 

        // Move content to bottom
        scrollRect.verticalNormalizedPosition = 0f;

        // Ensure Content is correctly positioned
        contentRect.anchoredPosition = new Vector2(contentRect.anchoredPosition.x, 0);
    }

}
