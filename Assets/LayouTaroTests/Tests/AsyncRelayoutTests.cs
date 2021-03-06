using System.Collections;
using UnityEngine;
using UILayouTaro;
using Miyamasu;
using System;
using System.Collections.Generic;
using NUnit.Framework;

public class AsyncRelayoutTests : MiyamasuTestRunner
{
    private Canvas canvas;

    [MSetup]
    public void Setup()
    {
        var canvasObj = GameObject.Find("Canvas");
        var cameraObj = GameObject.Find("Main Camera");
        if (canvasObj == null)
        {
            Camera camera = null;
            if (cameraObj == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            }

            // イベントシステムを追加してもボタンとかは効かないが、まあ、はい。
            // new GameObject("EventSystem").AddComponent<EventSystem>();

            canvas = new GameObject("TestCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            return;
        }

        throw new Exception("不要なデータが残っている");
    }

    [MTeardown]
    public void Teardown()
    {
        var canvasObj = GameObject.Find("TestCanvas");
        var cameraObj = GameObject.Find("Main Camera");
        GameObject.Destroy(canvasObj);
        GameObject.Destroy(cameraObj);
    }

    [MTest]
    public IEnumerator RelayoutWithEmojiAsync()
    {
        // generate your own data structure with parameters for UI.
        var box = AsyncBoxElement.GO(
            null,// UI bg with image
            () =>
            {
                Debug.Log("root box element is tapped.");
            },
            AsyncTextElement.GO("hannin is yasu! this is public problem\U0001F60A! gooooooooooooood "),// text.
            AsyncImageElement.GO(null),// image.
            AsyncButtonElement.GO(null, () => { Debug.Log("button is tapped."); })
        );

        // generate the layouter which you want to use for layout.
        var layouter = new BasicAsyncLayouter();

        // set the default size of content.
        var size = new Vector2(600, 100);

        // do layout with LayouTaro. the GameObject will be returned with layouted structure.

        yield return LayouTaro.LayoutAsync<BasicMissingSpriteCache>(
            canvas.transform,
            size,
            box,
            layouter
        );

        var rectTrans = box.gameObject.GetComponent<RectTransform>();
        rectTrans.anchoredPosition3D = Vector3.zero;
        rectTrans.localScale = Vector3.one;

        // update element values and re-layout with same GameObject.
        yield return LayouTaro.RelayoutWithUpdateAsync<BasicMissingSpriteCache>(
            size,
            box,
            new Dictionary<LTElementType, object> {
                {LTElementType.AsyncImage, null},
                {LTElementType.AsyncText, "relayout\U0001F60A!"}
            },
            layouter
        );

        while (false)
        {
            yield return null;
        }

        yield return null;

        ScreenCapture.CaptureScreenshot("./images/" + methodName);

        yield break;
    }

    [MTest]
    public IEnumerator RelayoutWithEmojiWithDelayAsync()
    {
        // generate your own data structure with parameters for UI.
        var box = AsyncBoxElement.GO(
            null,// UI bg with image
            () =>
            {
                Debug.Log("root box element is tapped.");
            },
            AsyncTextElement.GO("hannin is yasu! this is public problem\U0001F60A! gooooooooooooood "),// text.
            AsyncImageElement.GO(null),// image.
            AsyncButtonElement.GO(null, () => { Debug.Log("button is tapped."); })
        );

        // generate the layouter which you want to use for layout.
        var layouter = new BasicAsyncLayouter();

        // set the default size of content.
        var size = new Vector2(600, 100);

        // do layout with LayouTaro. the GameObject will be returned with layouted structure.

        yield return LayouTaro.LayoutAsync<BasicMissingSpriteCache>(
            canvas.transform,
            size,
            box,
            layouter
        );

        var rectTrans = box.gameObject.GetComponent<RectTransform>();
        rectTrans.anchoredPosition3D = Vector3.zero;
        rectTrans.localScale = Vector3.one;

        yield return null;

        // update element values and re-layout with same GameObject.
        yield return LayouTaro.RelayoutWithUpdateAsync<BasicMissingSpriteCache>(
            size,
            box,
            new Dictionary<LTElementType, object> {
                {LTElementType.AsyncImage, null},
                {LTElementType.AsyncText, "relayout\U0001F60A!"}
            },
            layouter
        );

        foreach (Transform childTrans in box.gameObject.transform)
        {
            foreach (Transform cousinTrans in childTrans)
            {
                var cousinRectTrans = cousinTrans.GetComponent<RectTransform>();
                Assert.IsTrue(cousinRectTrans.localScale.x == 1f, "not valid scale. actual:" + cousinRectTrans.localScale.x);
                Assert.IsTrue(cousinRectTrans.anchoredPosition3D.z == 0f, "not valid z pos. actual:" + cousinRectTrans.anchoredPosition3D.z);
            }
        }

        yield return null;

        ScreenCapture.CaptureScreenshot("./images/" + methodName);
        yield break;
    }

    [MTest]
    public IEnumerator RelayoutWithLongEmojiAsync()
    {
        // generate your own data structure with parameters for UI.
        var box = AsyncBoxElement.GO(
            null,// UI bg with image
            () =>
            {
                Debug.Log("root box element is tapped.");
            },
            AsyncTextElement.GO("\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00")
        );

        // generate the layouter which you want to use for layout.
        var layouter = new BasicAsyncLayouter();

        // set the default size of content.
        var size = new Vector2(600, 100);

        // do layout with LayouTaro. the GameObject will be returned with layouted structure.

        yield return LayouTaro.LayoutAsync<BasicMissingSpriteCache>(
            canvas.transform,
            size,
            box,
            layouter
        );

        var rectTrans = box.gameObject.GetComponent<RectTransform>();
        rectTrans.anchoredPosition3D = Vector3.zero;
        rectTrans.localScale = Vector3.one;

        // update element values and re-layout with same GameObject.
        yield return LayouTaro.RelayoutWithUpdateAsync<BasicMissingSpriteCache>(
            size,
            box,
            new Dictionary<LTElementType, object> {
                {LTElementType.AsyncText, "\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00\ud83d\ude00"}
            },
            layouter
        );

        yield return null;

        ScreenCapture.CaptureScreenshot("./images/" + methodName);

        yield break;
    }

}