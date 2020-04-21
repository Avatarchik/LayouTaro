
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UILayouTaro
{
    public class ParameterReference
    {
        public string id;// 判別用

        public float originX;
        public float originY;
        public float restWidth;
        public float currentLineMaxHeight;
        public List<RectTransform> lineContents;

        public ParameterReference(float originX, float originY, float restWidth, float currentLineMaxHeight, List<RectTransform> lineContents)
        {
            this.id = Guid.NewGuid().ToString();

            this.originX = originX;
            this.originY = originY;
            this.restWidth = restWidth;
            this.currentLineMaxHeight = currentLineMaxHeight;
            this.lineContents = lineContents;
        }

        public override string ToString()
        {
            return " originX:" + originX + " originY:" + originY + " restWidth:" + restWidth + " currentLineMaxHeight:" + currentLineMaxHeight;
        }
    }


    public static class BasicAsyncLayoutFunctions
    {
        public static AsyncLayoutOperation TextLayoutAsync<T, U>(T textElement, string contentText, RectTransform rectTrans, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            Debug.Assert(rectTrans.pivot.x == 0 && rectTrans.pivot.y == 1 && rectTrans.anchorMin.x == 0 && rectTrans.anchorMin.y == 1 && rectTrans.anchorMax.x == 0 && rectTrans.anchorMax.y == 1, "rectTransform for BasicAsyncLayoutFunctions.TextLayoutAsync should set pivot to 0,1 and anchorMin 0,1 anchorMax 0,1.");
            Debug.Assert(textElement.transform.childCount == 0, "BasicAsyncLayoutFunctions.TextLayoutAsync not allows text element which has child.");

            var refs = new ParameterReference(originX, originY, restWidth, currentLineMaxHeight, lineContents);

            // 文字がnullか空の場合、空のAsyncOpを返す。
            if (string.IsNullOrEmpty(contentText))
            {
                return new AsyncLayoutOperation(
                    rectTrans,
                    refs,
                    () =>
                    {
                        return (false, refs);
                    }
                );
            }

            var cor = _TextLayoutAsync<T, U>(textElement, contentText, rectTrans, viewWidth, refs);
            return new AsyncLayoutOperation(
                rectTrans,
                refs,
                () =>
                {
                    var cont = cor.MoveNext();
                    return (cont, refs);
                }
            );
        }

        public static AsyncLayoutOperation RectLayoutAsync<T>(LTAsyncElement rectElement, RectTransform rectTrans, Vector2 rectSize, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents) where T : IMissingSpriteCache, new()
        {
            var refs = new ParameterReference(originX, originY, restWidth, currentLineMaxHeight, lineContents);

            var cor = _RectLayoutAsync<T>(rectElement, rectTrans, rectSize, viewWidth, refs);
            return new AsyncLayoutOperation(
                rectTrans,
                refs,
                () =>
                {
                    var cont = cor.MoveNext();
                    return (cont, refs);
                }
            );
        }

        /*
            内部的な実装部
            どこかから先をstaticではないように作ると良さそう。キャッシュが切れる。まあTextにセットしてあるフォント単位でキャッシュ作っちゃうけどね。
        */
        private static IEnumerator _TextLayoutAsync<T, U>(T textElement, string contentText, RectTransform rectTrans, float viewWidth, ParameterReference refs) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            var continueContent = false;

        NextLine:
            var textComponent = textElement.GetComponent<TextMeshProUGUI>();
            TMPro.TMP_TextInfo textInfos = null;
            {
                // wordWrappingを可能にすると、表示はともかく実際にこの行にどれだけの文字が入っているか判断できる。
                textComponent.enableWordWrapping = true;
                textComponent.text = contentText;

                // 文字が入る箱のサイズを縦に無限にして、どの程度入るかのレイアウト情報を取得する。
                textComponent.rectTransform.sizeDelta = new Vector2(refs.restWidth, float.PositiveInfinity);
                textInfos = textComponent.GetTextInfo(contentText);

                // 文字を中央揃えではなく適正な位置にセットするためにラッピングを解除する。
                textComponent.enableWordWrapping = false;
            }

            // 絵文字や記号が含まれている場合、画像と文字に分けてレイアウトを行う。
            if (BasicLayoutFunctions.IsDetectEmojiAndMarkAndTextExist(contentText))
            {
                textComponent.text = string.Empty;

                // TMProが子オブジェクトを作っている場合があり、それらがあれば消す必要がある。
                // 同じフレーム内で同じ絵文字を作るようなことをする場合、作成されないケースがあるため、子供の有無を条件として絵文字の有無を判断することはできなかった。
                for (var i = 0; i < textComponent.transform.childCount; i++)
                {
                    GameObject.Destroy(textComponent.transform.GetChild(i).gameObject);
                }

                // 絵文字が含まれている。

                // 今後のレイアウトに自分自身を巻き込まないように、レイアウトから自分自身を取り外す
                refs.lineContents.RemoveAt(refs.lineContents.Count - 1);

                textComponent.rectTransform.sizeDelta = new Vector2(refs.restWidth, 0);// 高さが0で問題ない。

                // この内部で全てのレイアウトを終わらせる。
                var cor = LayoutContentWithEmojiAsync<T, U>(textElement, contentText, viewWidth, refs);
                while (cor.MoveNext())
                {
                    yield return null;
                }

                yield break;
            }

            var fontAsset = textComponent.font;
            if (!fontAsset.HasCharacters(contentText))
            {
                textComponent.text = string.Empty;

                // 今後のレイアウトに自分自身を巻き込まないように、レイアウトから自分自身を取り外す
                refs.lineContents.RemoveAt(refs.lineContents.Count - 1);

                textComponent.rectTransform.sizeDelta = new Vector2(refs.restWidth, 0);// 高さが0で問題ない。

                // missingを含んでいるので、内部でテキストとmissingに分解、レイアウトする。
                var cor = LayoutContentWithMissingTextAsync<T, U>(textElement, contentText, viewWidth, refs);
                while (cor.MoveNext())
                {
                    yield return null;
                }

                yield break;
            }

            var tmGeneratorLines = textInfos.lineInfo;
            var lineSpacing = textComponent.lineSpacing;
            var tmLineCount = textInfos.lineCount;

            var currentFirstLineWidth = tmGeneratorLines[0].length;
            var currentFirstLineHeight = tmGeneratorLines[0].lineHeight;

            var isHeadOfLine = refs.originX == 0;
            var isMultiLined = 1 < tmLineCount;
            var isLayoutedOutOfView = viewWidth < refs.originX + currentFirstLineWidth;

            var status = TextLayoutDefinitions.GetTextLayoutStatus(isHeadOfLine, isMultiLined, isLayoutedOutOfView);
            switch (status)
            {
                case TextLayoutStatus.NotHeadAndSingle:
                case TextLayoutStatus.HeadAndSingle:
                    {
                        // 全文を表示して終了
                        textComponent.text = contentText;
                        if (continueContent)
                        {
                            continueContent = false;
                            refs.restWidth = viewWidth - currentFirstLineWidth;
                            refs.currentLineMaxHeight = currentFirstLineHeight;
                        }
                        else
                        {
                            textComponent.rectTransform.anchoredPosition = new Vector2(refs.originX, refs.originY);
                        }

                        textComponent.rectTransform.sizeDelta = new Vector2(currentFirstLineWidth, currentFirstLineHeight);

                        ElementLayoutFunctions.ContinueLine(ref refs.originX, currentFirstLineWidth, currentFirstLineHeight, ref refs.currentLineMaxHeight);
                        break;
                    }

                case TextLayoutStatus.NotHeadAndMulti:
                    {
                        // textComponent.text = "<indent=" + originX + "pixels>"  でindentを付けられるが、これの恩恵を素直に受けられるケースが少ない。
                        // この行をレイアウトした際、行頭の文字のレイアウトが変わるようであれば、利用できない。
                        // 最適化としてはケースが複雑なので後回しにする。

                        // これは、この行 + 追加のHead ~ 系の複数のコンテンツに分割する。
                        var nextLineTextIndex = tmGeneratorLines[1].firstCharacterIndex;
                        var nextLineText = contentText.Substring(nextLineTextIndex);

                        // 現在の行のセット
                        textComponent.text = contentText.Substring(0, nextLineTextIndex);
                        textComponent.rectTransform.anchoredPosition = new Vector2(refs.originX, refs.originY);
                        textComponent.rectTransform.sizeDelta = new Vector2(currentFirstLineWidth, currentFirstLineHeight);

                        var childOriginX = refs.originX;
                        var currentTotalLineHeight = ElementLayoutFunctions.LineFeed(ref refs.originX, ref refs.originY, currentFirstLineHeight, ref refs.currentLineMaxHeight, ref refs.lineContents);// 文字コンテンツの高さ分改行する

                        // 次の行のコンテンツをこのコンテンツの子として生成するが、レイアウトまでを行わず次の行の起点の計算を行う。
                        // ここで全てを計算しない理由は、この処理の結果、複数種類のレイアウトが発生するため、ここで全てを書かない方が変えやすい。
                        {
                            // 末尾でgotoを使って次の行頭からのコンテンツの設置に行くので、計算に使う残り幅をビュー幅へとセットする。
                            refs.restWidth = viewWidth;

                            // 次の行のコンテンツを入れる
                            var nextLineTextElement = textElement.GenerateGO(nextLineText).GetComponent<T>();
                            nextLineTextElement.transform.SetParent(textElement.transform);// 消しやすくするため、この新規コンテンツを子にする

                            // xは-に、yは親の直下に置く。yは特に、「親が親の行上でどのように配置されたのか」を加味する必要がある。
                            // 例えば親行の中で親が最大の背の高さのコンテンツでない場合、改行すべき値は 親の背 + (行の背 - 親の背)/2 になる。

                            var yPosFromLinedParentY =
                                -(// 下向きがマイナスな座標系なのでマイナス
                                    currentFirstLineHeight// 現在の文字の高さと行の高さを比較し、文字の高さ + 上側の差の高さ(差/2)を足して返す。
                                    + (
                                        currentTotalLineHeight
                                        - currentFirstLineHeight
                                    )
                                    / 2
                                );

                            // X表示位置を原点にずらす、Yは次のコンテンツの開始Y位置 = LineFeedで変更された親の位置に依存し、親の位置からoriginYを引いた値になる。
                            var newTailTextElementRectTrans = nextLineTextElement.GetComponent<RectTransform>();
                            newTailTextElementRectTrans.anchoredPosition = new Vector2(-childOriginX, yPosFromLinedParentY);

                            // テキスト特有の継続したコンテンツ扱いする。
                            continueContent = true;

                            // 生成したコンテンツを次の行の要素へと追加する
                            refs.lineContents.Add(newTailTextElementRectTrans);

                            // 上書きを行う
                            textElement = nextLineTextElement;
                            contentText = nextLineText;
                            goto NextLine;
                        }
                    }

                case TextLayoutStatus.HeadAndMulti:
                    {
                        // このコンテンツは矩形で、行揃えの影響を受けないため、明示的に行から取り除く。
                        refs.lineContents.RemoveAt(refs.lineContents.Count - 1);

                        var lineCount = textInfos.lineCount;
                        var lineStrs = new string[lineCount];

                        var totalHeight = 0f;
                        for (var j = 0; j < lineCount; j++)
                        {
                            var lInfo = textInfos.lineInfo[j];
                            totalHeight += lInfo.lineHeight;

                            // ここで、含まれている絵文字の数がわかれば、そのぶんを後ろに足すことで文字切れを回避できそう、、ではある、、
                            lineStrs[j] = contentText.Substring(lInfo.firstCharacterIndex, lInfo.lastCharacterIndex - lInfo.firstCharacterIndex + 1);
                        }

                        // 矩形になるテキスト = 最終行を取り除いたテキストを得る
                        var rectTexts = new string[lineStrs.Length - 1];
                        for (var i = 0; i < rectTexts.Length; i++)
                        {
                            rectTexts[i] = lineStrs[i];
                        }

                        // 最終行のテキストを得る
                        var lastLineText = lineStrs[lineStrs.Length - 1];

                        // rectの高さの取得
                        var lastLineHeight = textInfos.lineInfo[lineCount - 1].lineHeight;
                        var rectHeight = totalHeight - lastLineHeight;

                        // 改行コードを入れ、複数行での表示をいい感じにする。
                        textComponent.text = string.Join("\n", rectTexts);
                        textComponent.rectTransform.sizeDelta = new Vector2(refs.restWidth, rectHeight);

                        // なんらかの続きの文字コンテンツである場合、そのコンテンツの子になっているので位置情報を調整しない。最終行を分割する。
                        if (continueContent)
                        {
                            // 別のコンテンツから継続している行はじめの処理なので、子をセットする前にここまでの分の改行を行う。
                            ElementLayoutFunctions.LineFeed(ref refs.originX, ref refs.originY, rectHeight, ref refs.currentLineMaxHeight, ref refs.lineContents);

                            // 末尾でgotoを使って次の行頭からのコンテンツの設置に行くので、計算に使う残り幅をビュー幅へとセットする。
                            refs.restWidth = viewWidth;

                            // 最終行のコンテンツを入れる
                            var nextLineTextElement = textElement.GenerateGO(lastLineText).GetComponent<T>();
                            nextLineTextElement.transform.SetParent(textElement.transform.parent);// 消しやすくするため、この新規コンテンツを現在の要素の親の子にする

                            // 次の行の行頭になる = 続いている要素と同じxを持つ
                            var childX = textComponent.rectTransform.anchoredPosition.x;

                            // yは親の分移動する
                            var childY = textComponent.rectTransform.anchoredPosition.y - rectHeight;

                            var newBrotherTailTextElementRectTrans = nextLineTextElement.GetComponent<RectTransform>();
                            newBrotherTailTextElementRectTrans.anchoredPosition = new Vector2(childX, childY);

                            // 継続させる
                            continueContent = true;

                            // 生成したコンテンツを次の行の要素へと追加する
                            refs.lineContents.Add(newBrotherTailTextElementRectTrans);

                            // 上書きを行う
                            textElement = nextLineTextElement;
                            contentText = lastLineText;
                            goto NextLine;
                        }

                        // 誰かの子ではないので、独自に自分の位置をセットする
                        textComponent.rectTransform.anchoredPosition = new Vector2(refs.originX, refs.originY);

                        // 最終行のコンテンツを入れる
                        var newTextElement = textElement.GenerateGO(lastLineText).GetComponent<T>();
                        newTextElement.transform.SetParent(rectTrans);// 消しやすくするため、この新規コンテンツを現在の要素の子にする

                        // 残りの行のサイズは最大化する
                        refs.restWidth = viewWidth;

                        // 次の行の行頭になる
                        refs.originX = 0;

                        // yは親の分移動する
                        refs.originY -= rectHeight;

                        var newTailTextElementRectTrans = newTextElement.GetComponent<RectTransform>();
                        newTailTextElementRectTrans.anchoredPosition = new Vector2(refs.originX, refs.originY);

                        // 残りのデータをテキスト特有の継続したコンテンツ扱いする。
                        continueContent = true;

                        // 生成したコンテンツを次の行の要素へと追加する
                        refs.lineContents.Add(newTailTextElementRectTrans);

                        textElement = newTextElement;
                        contentText = lastLineText;
                        goto NextLine;
                    }
                case TextLayoutStatus.NotHeadAndOutOfView:
                    {
                        // 次の行にコンテンツを置き、継続する

                        // 現在最後の追加要素である自分自身を取り出し、ここまでの行の要素を整列させる。
                        refs.lineContents.RemoveAt(refs.lineContents.Count - 1);
                        ElementLayoutFunctions.LineFeed(ref refs.originX, ref refs.originY, refs.currentLineMaxHeight, ref refs.currentLineMaxHeight, ref refs.lineContents);

                        // レイアウト対象のビューサイズを新しい行のものとして更新する
                        refs.restWidth = viewWidth;
                        refs.lineContents.Add(textComponent.rectTransform);
                        goto NextLine;
                    }
            }
            yield break;
        }

        private static IEnumerator LayoutContentWithEmojiAsync<T, U>(T textElement, string contentText, float viewWidth, ParameterReference refs) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            /*
                絵文字が含まれている文字列を、絵文字と矩形に分解、再構成を行う。絵文字を単に画像が入る箱としてRectLayoutに放り込む。

                自分自身を書き換えて、一連のコマンドを実行するようにする。
                文字がどう始まるかも含めて、今足されているlinedからは一度離反する。その上で一つ目のコンテンツを追加する。
            */
            var elementsWithEmoji = CollectEmojiAndMarkAndTextElement<T, U>(textElement, contentText);

            for (var i = 0; i < elementsWithEmoji.Count; i++)
            {
                var element = elementsWithEmoji[i];
                var rectTrans = element.GetComponent<RectTransform>();
                refs.restWidth = viewWidth - refs.originX;
                refs.lineContents.Add(rectTrans);

                if (element is InternalAsyncEmojiRect)
                {
                    // emojiRectが入っている
                    var internalRectElement = (InternalAsyncEmojiRect)element;
                    var cor = _EmojiRectLayoutAsync<U>(internalRectElement, rectTrans, viewWidth, refs);

                    while (cor.MoveNext())
                    {
                        yield return null;
                    }

                    continue;
                }

                // ここに来るということは、T型が入っている。
                var internalTextElement = (T)element;
                var internalContentText = internalTextElement.Text();

                var textCor = _TextLayoutAsync<T, U>(internalTextElement, internalContentText, rectTrans, viewWidth, refs);
                while (textCor.MoveNext())
                {
                    yield return null;
                }
            }

            yield break;
        }

        private static List<LTAsyncElement> CollectEmojiAndMarkAndTextElement<T, U>(T textElement, string contentText) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            var elementsWithEmoji = new List<LTAsyncElement>();

            var textStartIndex = 0;
            var length = 0;
            for (var i = 0; i < contentText.Length; i++)
            {
                var firstChar = contentText[i];

                // \u26A1
                var isSymbol = Char.IsSymbol(firstChar);
                if (isSymbol)
                {
                    if (0 < length)
                    {
                        var currentText = contentText.Substring(textStartIndex, length);
                        var newTextElement = textElement.GenerateGO(currentText).GetComponent<T>();
                        newTextElement.transform.SetParent(textElement.transform, false);
                        elementsWithEmoji.Add(newTextElement);
                    }

                    length = 0;

                    // 記号確定。なので、要素として扱い、次の文字を飛ばす処理を行う。
                    var emojiElement = InternalAsyncEmojiRect.New<T, U>(textElement, new Char[] { firstChar });
                    elementsWithEmoji.Add(emojiElement);

                    // 文字は次から始まる、、かもしれない。
                    textStartIndex = i + 1;
                    continue;
                }

                // \U0001F971
                var isSurrogate = Char.IsSurrogate(firstChar);
                if (isSurrogate)
                {
                    if (0 < length)
                    {
                        var currentText = contentText.Substring(textStartIndex, length);
                        var newTextElement = textElement.GenerateGO(currentText).GetComponent<T>();
                        newTextElement.transform.SetParent(textElement.transform, false);
                        elementsWithEmoji.Add(newTextElement);
                    }

                    length = 0;

                    if (i == contentText.Length - 1)
                    {
                        // 続きの文字がないのでサロゲートペアではない。無視する。
                        // 文字の開始インデックスを次の文字へとセットする。
                        textStartIndex = i + 1;
                        continue;
                    }

                    var nextChar = contentText[i + 1];
                    var isSurrogatePair = Char.IsSurrogatePair(firstChar, nextChar);

                    if (isSurrogatePair)
                    {
                        // サロゲートペア確定。なので、要素として扱い、次の文字を飛ばす処理を行う。
                        var emojiElement = InternalAsyncEmojiRect.New<T, U>(textElement, new Char[] { firstChar, nextChar });
                        elementsWithEmoji.Add(emojiElement);

                        // 文字は次の次から始まる、、かもしれない。
                        textStartIndex = i + 2;
                        i = i + 1;
                        continue;
                    }

                    // ペアではなかったので、無視して次の文字へと行く。
                    textStartIndex = i + 1;
                    continue;
                }

                // サロゲートではないので文字として扱う
                length++;
            }

            // 残りの文字を足す
            if (0 < length)
            {
                var lastText = contentText.Substring(textStartIndex, length);
                var lastTextElement = textElement.GenerateGO(lastText).GetComponent<T>();
                lastTextElement.transform.SetParent(textElement.transform, false);
                elementsWithEmoji.Add(lastTextElement);
            }

            return elementsWithEmoji;
        }




        private static IEnumerator LayoutContentWithMissingTextAsync<T, U>(T textElement, string contentText, float viewWidth, ParameterReference refs) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            /*
                missingな文字が含まれている文字列を、文字と矩形に分解、再構成を行う。missing文字を単に画像が入る箱としてRectLayoutに放り込む。
                文字がどう始まるかも含めて、今足されているlinedからは一度離反する。その上で一つ目のコンテンツを追加する。
            */
            var elementsWithMissing = CollectMissingAndTextElement<T, U>(textElement, contentText);

            for (var i = 0; i < elementsWithMissing.Count; i++)
            {
                var element = elementsWithMissing[i];
                var rectTrans = element.GetComponent<RectTransform>();
                refs.restWidth = viewWidth - refs.originX;
                refs.lineContents.Add(rectTrans);

                if (element is InternalAsyncMissingTextRect)
                {
                    // missingTextRectが入っている
                    var internalRectElement = (InternalAsyncMissingTextRect)element;
                    var cor = _MissingTextRectLayoutAsync<U>(internalRectElement, rectTrans, viewWidth, refs);

                    while (cor.MoveNext())
                    {
                        yield return null;
                    }

                    continue;
                }

                // ここに来るということは、T型が入っている。
                var internalTextElement = (T)element;
                var internalContentText = internalTextElement.Text();

                var textCor = _TextLayoutAsync<T, U>(internalTextElement, internalContentText, rectTrans, viewWidth, refs);
                while (textCor.MoveNext())
                {
                    yield return null;
                }
            }

            yield break;
        }

        private static List<LTAsyncElement> CollectMissingAndTextElement<T, U>(T textElement, string contentText) where T : LTAsyncElement, ILayoutableText where U : IMissingSpriteCache, new()
        {
            var elementsWithMissing = new List<LTAsyncElement>();
            var font = textElement.GetComponent<TextMeshProUGUI>().font;

            var textStartIndex = 0;
            var length = 0;
            for (var i = 0; i < contentText.Length; i++)
            {
                var firstChar = contentText[i];

                var isExist = TextLayoutDefinitions.TMPro_ChechIfTextCharacterExist(font, firstChar);
                if (!isExist)
                {
                    // missingにぶち当たった。ここまでに用意されているテキストを取り出す
                    if (0 < length)
                    {
                        var currentText = contentText.Substring(textStartIndex, length);
                        var newTextElement = textElement.GenerateGO(currentText).GetComponent<T>();
                        newTextElement.transform.SetParent(textElement.transform, false);
                        elementsWithMissing.Add(newTextElement);
                    }

                    length = 0;

                    // missing文字確定。なので、箱的な要素として扱い、次の文字を飛ばす処理を行う。
                    var missingTextElement = InternalAsyncMissingTextRect.New<T, U>(textElement, contentText.Substring(i, 1));
                    elementsWithMissing.Add(missingTextElement);

                    // 文字は次から始まる、、かもしれない。
                    textStartIndex = i + 1;
                    continue;
                }

                // missingではないので文字として扱う
                length++;
            }

            // 残りの文字を足す
            if (0 < length)
            {
                var lastText = contentText.Substring(textStartIndex, length);
                var lastTextElement = textElement.GenerateGO(lastText).GetComponent<T>();
                lastTextElement.transform.SetParent(textElement.transform, false);
                elementsWithMissing.Add(lastTextElement);
            }

            return elementsWithMissing;
        }





        private static IEnumerator _EmojiRectLayoutAsync<T>(InternalAsyncEmojiRect rectElement, RectTransform transform, float viewWidth, ParameterReference refs) where T : IMissingSpriteCache, new()
        {
            // ここでサイズを確定させている。レイアウト対象の画像が存在していれば、GOを作成したタイミングでサイズが確定されているが、
            // もし対象が見つかっていない場合、このelementは既に通信を行っている。その場合、ここで完了を待つことができる。

            while (rectElement.IsLoading)
            {
                yield return null;
            }

            var rectSize = rectElement.RectSize();
            var cor = _RectLayoutAsync<T>(rectElement, transform, rectSize, viewWidth, refs);
            while (cor.MoveNext())
            {
                yield return null;
            }
        }

        private static IEnumerator _MissingTextRectLayoutAsync<T>(InternalAsyncMissingTextRect rectElement, RectTransform transform, float viewWidth, ParameterReference refs) where T : IMissingSpriteCache, new()
        {
            // ここでサイズを確定させている。レイアウト対象の画像が存在していれば、GOを作成したタイミングでサイズが確定されているが、
            // もし対象が見つかっていない場合、このelementは既に通信を行っている。その場合、ここで完了を待つことができる。

            while (rectElement.IsLoading)
            {
                yield return null;
            }

            var rectSize = rectElement.RectSize();
            var cor = _RectLayoutAsync<T>(rectElement, transform, rectSize, viewWidth, refs);
            while (cor.MoveNext())
            {
                yield return null;
            }
        }

        private static IEnumerator _RectLayoutAsync<T>(LTAsyncElement rectElement, RectTransform transform, Vector2 rectSize, float viewWidth, ParameterReference refs) where T : IMissingSpriteCache, new()
        {
            Debug.Assert(transform.pivot.x == 0 && transform.pivot.y == 1 && transform.anchorMin.x == 0 && transform.anchorMin.y == 1 && transform.anchorMax.x == 0 && transform.anchorMax.y == 1, "rectTransform for LayouTaro should set pivot to 0,1 and anchorMin 0,1 anchorMax 0,1.");

            if (refs.restWidth < rectSize.x)// 同じ列にレイアウトできないので次の列に行く。
            {
                // 現在最後の追加要素である自分自身を取り出し、整列させる。
                refs.lineContents.RemoveAt(refs.lineContents.Count - 1);
                ElementLayoutFunctions.LineFeed(ref refs.originX, ref refs.originY, refs.currentLineMaxHeight, ref refs.currentLineMaxHeight, ref refs.lineContents);
                refs.lineContents.Add(transform);

                // 位置をセット
                transform.anchoredPosition = new Vector2(refs.originX, refs.originY);
            }
            else
            {
                // 位置をセット
                transform.anchoredPosition = new Vector2(refs.originX, refs.originY);
            }

            // ジャストで埋まったら、次の行を作成する。
            if (refs.restWidth == rectSize.x)
            {
                ElementLayoutFunctions.LineFeed(ref refs.originX, ref refs.originY, transform.sizeDelta.y, ref refs.currentLineMaxHeight, ref refs.lineContents);

                refs.restWidth = viewWidth;
                yield break;
            }

            ElementLayoutFunctions.ContinueLine(ref refs.originX, rectSize.x, transform.sizeDelta.y, ref refs.currentLineMaxHeight);
            refs.restWidth = viewWidth - refs.originX;
            yield break;
        }
    }
}