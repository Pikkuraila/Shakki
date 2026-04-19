using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Presentation;

namespace Shakki.Core
{
    public class RandomResolutionService : MonoBehaviour
    {
        public static RandomResolutionService Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private RollOverlayView overlayView;
        [SerializeField] private Sprite coinSuccessSprite;
        [SerializeField] private Sprite coinFailSprite;
        [SerializeField] private Sprite dieSuccessSprite;
        [SerializeField] private Sprite dieFailSprite;

        [Header("Timing")]
        [SerializeField] private float displayDuration = 1f;
        [SerializeField] private float preRollDelay = 0.1f;

        private readonly Queue<RandomRollRequest> _pendingRequests = new();
        private bool _isResolving;
        public bool IsBusy => _isResolving || _pendingRequests.Count > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void RequestRoll(RandomRollRequest request)
        {
            if (request == null)
                return;

            _pendingRequests.Enqueue(request);
            if (!_isResolving)
                StartCoroutine(CoResolveQueue());
        }

        private IEnumerator CoResolveQueue()
        {
            _isResolving = true;

            while (_pendingRequests.Count > 0)
            {
                var request = _pendingRequests.Dequeue();
                yield return CoResolveSingle(request);
            }

            _isResolving = false;
        }

        private IEnumerator CoResolveSingle(RandomRollRequest request)
        {
            Debug.Log($"[Roll] {request.label} rolling d{request.sides}...");

            if (preRollDelay > 0f)
                yield return new WaitForSecondsRealtime(preRollDelay);

            int raw = Random.Range(1, request.sides + 1);
            int finalValue = raw + request.modifier;

            bool success = request.higherOrEqualWins
                ? finalValue >= request.targetValue
                : finalValue <= request.targetValue;

            request.onResolved?.Invoke(finalValue);

            Debug.Log($"[Roll] {request.label} raw={raw} modifier={request.modifier} final={finalValue} success={success}");

            Sprite resultSprite = GetResultSprite(request.visualType, success);

            if (overlayView != null && resultSprite != null)
                overlayView.Show(resultSprite);

            float timer = 0f;
            while (timer < displayDuration)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (overlayView != null)
                overlayView.Hide();

            if (success)
                request.onSuccess?.Invoke();
            else
                request.onFail?.Invoke();
        }

        private Sprite GetResultSprite(RollVisualType visualType, bool success)
        {
            switch (visualType)
            {
                case RollVisualType.Coin:
                    return success ? coinSuccessSprite : coinFailSprite;

                case RollVisualType.Die:
                    return success ? dieSuccessSprite : dieFailSprite;

                default:
                    return null;
            }
        }
    }
}
