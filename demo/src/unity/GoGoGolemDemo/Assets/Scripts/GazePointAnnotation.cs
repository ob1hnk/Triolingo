// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using Mediapipe;
using Mediapipe.Unity.CoordinateSystem;
using UnityEngine;
using mptcc = Mediapipe.Tasks.Components.Containers;
using UnityColor = UnityEngine.Color;
using UnityImage = UnityEngine.UI.Image;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  [RequireComponent(typeof(RectTransform))]
  public sealed class GazePointAnnotation : HierarchicalAnnotation
  {
    [SerializeField] private UnityImage _indicatorImage;
    [SerializeField] private SpriteRenderer _indicatorSprite;
    [SerializeField] private UnityColor _color = UnityColor.cyan;
    [SerializeField, Min(1f)] private float _radius = 20f;

    private RectTransform _rectTransform;

    private void Awake()
    {
      _rectTransform = GetComponent<RectTransform>();
      if (_rectTransform == null)
      {
        _rectTransform = gameObject.AddComponent<RectTransform>();
      }
      
      // 시선 포인트는 화면 내 특정 위치에 표시되므로 anchor를 중앙으로 설정
      _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
      _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
      _rectTransform.pivot = new Vector2(0.5f, 0.5f);
      
      // Scale이 1이 아니면 위치 계산이 왜곡되므로 강제로 1로 설정
      transform.localScale = Vector3.one;

      _indicatorImage ??= GetComponent<UnityImage>();
      _indicatorSprite ??= GetComponent<SpriteRenderer>();

      // UI Image가 없으면 생성하고 원형 스프라이트 설정
      if (_indicatorImage == null)
      {
        _indicatorImage = gameObject.AddComponent<UnityImage>();
        _indicatorImage.sprite = CreateCircleSprite();
        _indicatorImage.type = UnityEngine.UI.Image.Type.Simple;
        _indicatorImage.preserveAspect = true;
      }
      else if (_indicatorImage.sprite == null)
      {
        _indicatorImage.sprite = CreateCircleSprite();
      }

      if (_indicatorSprite != null)
      {
        _indicatorSprite.enabled = false;
      }

      if (_indicatorImage != null) { _indicatorImage.raycastTarget = false; }
      ApplyColor(_color);
      ApplyRadius(_radius);
    }

    private void OnEnable()
    {
      ApplyColor(_color);
      ApplyRadius(_radius);
    }

    public void SetColor(UnityColor color)
    {
      _color = color;
      ApplyColor(_color);
    }

    public void SetRadius(float radius)
    {
      _radius = Mathf.Max(1f, radius);
      ApplyRadius(_radius);
    }

    public void Draw(NormalizedLandmark target, bool visualizeZ = true)
    {
      if (ActivateFor(target))
      {
        UpdatePosition(GetScreenRect().GetPoint(target, rotationAngle, isMirrored), visualizeZ);
      }
    }

    public void Draw(in mptcc.NormalizedLandmark target, bool visualizeZ = true)
    {
      if (ActivateFor(target))
      {
        var position = GetScreenRect().GetPoint(in target, rotationAngle, isMirrored);
        UpdatePosition(position, visualizeZ);
      }
    }

    private void UpdatePosition(Vector3 position, bool visualizeZ)
    {
      if (!visualizeZ)
      {
        position.z = 0f;
      }
      // RectTransform을 사용하므로 anchoredPosition을 사용
      if (_rectTransform != null)
      {
        _rectTransform.anchoredPosition3D = position;
      }
      else
      {
        transform.localPosition = position;
      }
    }

    private void ApplyColor(UnityColor color)
    {
      if (_indicatorImage != null) { _indicatorImage.color = color; }
      if (_indicatorSprite != null) { _indicatorSprite.color = color; }
    }

    private void ApplyRadius(float radius)
    {
      if (_rectTransform != null)
      {
        _rectTransform.sizeDelta = Vector2.one * radius * 2f;
      }
    }

    /// <summary>
    ///   원형 스프라이트를 동적으로 생성합니다.
    /// </summary>
    private Sprite CreateCircleSprite()
    {
      const int textureSize = 64;
      var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
      var center = textureSize / 2f;
      var radius = textureSize / 2f - 1f;

      var colors = new Color32[textureSize * textureSize];
      for (int y = 0; y < textureSize; y++)
      {
        for (int x = 0; x < textureSize; x++)
        {
          var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
          var alpha = distance <= radius ? 1f : 0f;
          // 부드러운 가장자리를 위한 안티앨리어싱
          if (distance > radius && distance <= radius + 1f)
          {
            alpha = 1f - (distance - radius);
          }
          colors[y * textureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
        }
      }

      texture.SetPixels32(colors);
      texture.Apply();

      return Sprite.Create(texture, new UnityEngine.Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
    }
  }
}

