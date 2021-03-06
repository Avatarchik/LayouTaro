using System;
using System.Collections.Generic;
using UILayouTaro;
using UnityEngine;
using UnityEngine.UI;

public class BasicAsyncLayouter : IAsyncLayouter
{
    /*
        子要素をレイアウトし、親要素が余白ありでそれを包む。
        outを使いたいから、非同期な計算を行う実行体をここから返すようにする。
    */
    public List<AsyncLayoutOperation> LayoutAsync<T>(ref Vector2 viewSize, out float originX, out float originY, GameObject rootObject, LTAsyncRootElement rootElement, LTAsyncElement[] elements, ref float currentLineMaxHeight, ref List<RectTransform> lineContents) where T : IMissingSpriteCache, new()
    {
        var outsideSpacing = 10f;

        originX = 0f;
        originY = 0f;

        var originalViewWidth = viewSize.x;

        viewSize.x = viewSize.x - outsideSpacing * 2;// 左右の余白分を引く
        var viewWidth = viewSize.x;

        // MyLayoutはrootとしてboxがくる前提で作られている、という想定のサンプル
        var root = rootObject.GetComponent<AsyncBoxElement>();
        var rootTrans = root.GetComponent<RectTransform>();

        var layoutOps = new List<AsyncLayoutOperation>();

        for (var i = 0; i < elements.Length; i++)
        {
            var element = elements[i];

            var currentElementRectTrans = element.GetComponent<RectTransform>();
            var restWidth = viewSize.x - originX;

            var type = element.GetLTElementType();
            switch (type)
            {
                case LTElementType.AsyncImage:
                    var imageElement = (AsyncImageElement)element;

                    // 概念的に、後でレイアウトする対象をレイアウト処理順にAddしている。
                    layoutOps.Add(
                        BasicAsyncLayoutFunctions.RectLayoutAsync<T>(
                            imageElement,
                            currentElementRectTrans,
                            imageElement.RectSize(),
                            viewWidth,
                            ref originX,
                            ref originY,
                            ref restWidth,
                            ref currentLineMaxHeight,
                            ref lineContents
                        )
                    );
                    break;
                case LTElementType.AsyncText:
                    var newTailTextElement = (AsyncTextElement)element;
                    var contentText = newTailTextElement.Text();

                    layoutOps.Add(
                        BasicAsyncLayoutFunctions.TextLayoutAsync<AsyncTextElement, T>(
                            newTailTextElement,
                            contentText,
                            currentElementRectTrans,
                            viewWidth,
                            ref originX,
                            ref originY,
                            ref restWidth,
                            ref currentLineMaxHeight,
                            ref lineContents
                        )
                    );
                    break;
                case LTElementType.AsyncText2:
                    var newTailTextElement2 = (AsyncTextElement2)element;
                    var contentText2 = newTailTextElement2.Text();

                    layoutOps.Add(
                        BasicAsyncLayoutFunctions.TextLayoutAsync<AsyncTextElement2, T>(
                            newTailTextElement2,
                            contentText2,
                            currentElementRectTrans,
                            viewWidth,
                            ref originX,
                            ref originY,
                            ref restWidth,
                            ref currentLineMaxHeight,
                            ref lineContents
                        )
                    );
                    break;
                case LTElementType.AsyncText3:
                    var newTailTextElement3 = (AsyncTextElement3)element;
                    var contentText3 = newTailTextElement3.Text();

                    layoutOps.Add(
                        BasicAsyncLayoutFunctions.TextLayoutAsync<AsyncTextElement3, T>(
                            newTailTextElement3,
                            contentText3,
                            currentElementRectTrans,
                            viewWidth,
                            ref originX,
                            ref originY,
                            ref restWidth,
                            ref currentLineMaxHeight,
                            ref lineContents
                        )
                    );
                    break;
                case LTElementType.AsyncButton:
                    var buttonElement = (AsyncButtonElement)element;

                    layoutOps.Add(
                        BasicAsyncLayoutFunctions.RectLayoutAsync<T>(
                            buttonElement,
                            currentElementRectTrans,
                            buttonElement.RectSize(),
                            viewWidth,
                            ref originX,
                            ref originY,
                            ref restWidth,
                            ref currentLineMaxHeight,
                            ref lineContents
                        )
                    );

                    break;

                case LTElementType.AsyncBox:
                    throw new Exception("unsupported layout:" + type);// 子のレイヤーにBoxが来るのを許可しない。

                default:
                    Debug.LogError("unsupported element type:" + type);
                    break;
            }
        }

        return layoutOps;
    }

    /*
        layout後、LayouTaroから呼ばれる
    */
    public void AfterLayout(Vector2 viewSize, float originX, float originY, GameObject rootObject, LTAsyncRootElement rootElement, LTAsyncElement[] elements, ref float currentLineMaxHeight, ref List<RectTransform> lastLineContents, Vector2 wrappedSize)
    {
        // 最終行の整列を行う
        BasicAsyncLayoutFunctions.LayoutLastLine<LTAsyncRootElement>(ref originY, currentLineMaxHeight, ref lastLineContents);

        var outsideSpacing = 10f;

        var rootTrans = rootObject.GetComponent<RectTransform>();

        // boxのサイズを調整する
        rootTrans.sizeDelta = new Vector2(wrappedSize.x + outsideSpacing * 2, wrappedSize.y + outsideSpacing * 2);// オリジナル幅で、幅と高さに対して2倍分の余白を足す。

        // 子要素の余白分の移動
        foreach (var e in elements)
        {
            var rectTrans = e.GetComponent<RectTransform>();
            rectTrans.anchoredPosition = new Vector2(rectTrans.anchoredPosition.x + outsideSpacing, rectTrans.anchoredPosition.y - outsideSpacing);// ルートの下のエレメントの要素をスペース分移動する。yは-なので-する。
        }
    }

    public void UpdateValuesAsync(LTAsyncElement[] elements, Dictionary<LTElementType, object> updateValues)
    {
        foreach (var e in elements)
        {
            var key = e.GetLTElementType();
            if (!updateValues.ContainsKey(key))
            {
                continue;
            }

            switch (key)
            {
                case LTElementType.AsyncImage:
                    var i = (AsyncImageElement)e;

                    // get value from updateValues and cast to the type what you set.
                    var p = updateValues[LTElementType.AsyncImage] as Image;
                    i.Image = p;

                    break;
                case LTElementType.AsyncText:
                    var t = (AsyncTextElement)e;

                    // get value from updateValues and cast to the type what you set.
                    var tVal = updateValues[LTElementType.AsyncText] as string;
                    t.TextContent = tVal;
                    break;
                case LTElementType.AsyncText2:
                    var t2 = (AsyncTextElement2)e;

                    // get value from updateValues and cast to the type what you set.
                    var tVal2 = updateValues[LTElementType.AsyncText2] as string;
                    t2.TextContent = tVal2;
                    break;
                case LTElementType.AsyncText3:
                    var t3 = (AsyncTextElement3)e;

                    // get value from updateValues and cast to the type what you set.
                    var tVal3 = updateValues[LTElementType.AsyncText3] as string;
                    t3.TextContent = tVal3;
                    break;

                default:
                    break;
            }
        }
    }
}