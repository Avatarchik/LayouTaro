
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UILayouTaro
{
    public static class BasicLayoutFunctions
    {

        public static void TextLayout<T>(T textElement, string contentText, RectTransform rectTrans, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents) where T : LTElement, ILayoutableText
        {
            // 文字列が空な場合、何もせずに返す。
            if (string.IsNullOrEmpty(contentText))
            {
                return;
            }

            Debug.Assert(rectTrans.pivot.x == 0 && rectTrans.pivot.y == 1 && rectTrans.anchorMin.x == 0 && rectTrans.anchorMin.y == 1 && rectTrans.anchorMax.x == 0 && rectTrans.anchorMax.y == 1, "rectTransform for BasicLayoutFunctions.TextLayout should set pivot to 0,1 and anchorMin 0,1 anchorMax 0,1.");
            Debug.Assert(textElement.transform.childCount == 0, "BasicLayoutFunctions.TextLayout not allows text element which has child.");
            var continueContent = false;

        NextLine:
            var textComponent = textElement.GetComponent<TextMeshProUGUI>();
            TMPro.TMP_TextInfo textInfos = null;
            {
                // wordWrappingを可能にすると、表示はともかく実際にこの行にどれだけの文字が入っているか判断できる。
                textComponent.enableWordWrapping = true;
                textComponent.text = contentText;

                // 文字が入る箱のサイズを縦に無限にして、どの程度入るかのレイアウト情報を取得する。
                textComponent.rectTransform.sizeDelta = new Vector2(restWidth, float.PositiveInfinity);
                textInfos = textComponent.GetTextInfo(contentText);

                // 文字を中央揃えではなく適正な位置にセットするためにラッピングを解除する。
                textComponent.enableWordWrapping = false;
            }

            // 絵文字や記号が含まれている場合、画像と文字に分けてレイアウトを行う。
            if (IsDetectEmojiAndMarkAndTextExist(contentText))
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
                lineContents.RemoveAt(lineContents.Count - 1);

                textComponent.rectTransform.sizeDelta = new Vector2(restWidth, 0);// 高さが0で問題ない。

                // この内部で全てのレイアウトを終わらせる。
                LayoutContentWithEmoji(textElement, contentText, viewWidth, ref originX, ref originY, ref restWidth, ref currentLineMaxHeight, ref lineContents);
                return;
            }

            var fontAsset = textComponent.font;
            List<char> missingCharacters;
            if (!fontAsset.HasCharacters(contentText, out missingCharacters))
            {
                // missingCharactersを送り出す
                LayouTaro._OnMissingCharacter(missingCharacters.ToArray());
            }

            var tmGeneratorLines = textInfos.lineInfo;
            var lineSpacing = textComponent.lineSpacing;
            var tmLineCount = textInfos.lineCount;

            var currentFirstLineWidth = tmGeneratorLines[0].length;
            var currentFirstLineHeight = tmGeneratorLines[0].lineHeight;

            var isHeadOfLine = originX == 0;
            var isMultiLined = 1 < tmLineCount;
            var isLayoutedOutOfView = viewWidth < originX + currentFirstLineWidth;

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
                            restWidth = viewWidth - currentFirstLineWidth;
                            currentLineMaxHeight = currentFirstLineHeight;
                        }
                        else
                        {
                            textComponent.rectTransform.anchoredPosition = new Vector2(originX, originY);
                        }

                        textComponent.rectTransform.sizeDelta = new Vector2(currentFirstLineWidth, currentFirstLineHeight);

                        ElementLayoutFunctions.ContinueLine(ref originX, currentFirstLineWidth, currentFirstLineHeight, ref currentLineMaxHeight);
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
                        textComponent.rectTransform.anchoredPosition = new Vector2(originX, originY);
                        textComponent.rectTransform.sizeDelta = new Vector2(currentFirstLineWidth, currentFirstLineHeight);

                        var childOriginX = originX;
                        var currentTotalLineHeight = ElementLayoutFunctions.LineFeed(ref originX, ref originY, currentFirstLineHeight, ref currentLineMaxHeight, ref lineContents);// 文字コンテンツの高さ分改行する

                        // 次の行のコンテンツをこのコンテンツの子として生成するが、レイアウトまでを行わず次の行の起点の計算を行う。
                        // ここで全てを計算しない理由は、この処理の結果、複数種類のレイアウトが発生するため、ここで全てを書かない方が変えやすい。
                        {
                            // 末尾でgotoを使って次の行頭からのコンテンツの設置に行くので、計算に使う残り幅をビュー幅へとセットする。
                            restWidth = viewWidth;

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
                            lineContents.Add(newTailTextElementRectTrans);

                            // 上書きを行う
                            textElement = nextLineTextElement;
                            contentText = nextLineText;
                            goto NextLine;
                        }
                    }

                case TextLayoutStatus.HeadAndMulti:
                    {
                        // このコンテンツは矩形で、行揃えの影響を受けないため、明示的に行から取り除く。
                        lineContents.RemoveAt(lineContents.Count - 1);

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
                        textComponent.rectTransform.sizeDelta = new Vector2(restWidth, rectHeight);

                        // なんらかの続きの文字コンテンツである場合、そのコンテンツの子になっているので位置情報を調整しない。最終行を分割する。
                        if (continueContent)
                        {
                            // 別のコンテンツから継続している行はじめの処理なので、子をセットする前にここまでの分の改行を行う。
                            ElementLayoutFunctions.LineFeed(ref originX, ref originY, rectHeight, ref currentLineMaxHeight, ref lineContents);

                            // 末尾でgotoを使って次の行頭からのコンテンツの設置に行くので、計算に使う残り幅をビュー幅へとセットする。
                            restWidth = viewWidth;

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
                            lineContents.Add(newBrotherTailTextElementRectTrans);

                            // 上書きを行う
                            textElement = nextLineTextElement;
                            contentText = lastLineText;
                            goto NextLine;
                        }

                        // 誰かの子ではないので、独自に自分の位置をセットする
                        textComponent.rectTransform.anchoredPosition = new Vector2(originX, originY);

                        // 最終行のコンテンツを入れる
                        var newTextElement = textElement.GenerateGO(lastLineText).GetComponent<T>();
                        newTextElement.transform.SetParent(rectTrans);// 消しやすくするため、この新規コンテンツを現在の要素の子にする

                        // 残りの行のサイズは最大化する
                        restWidth = viewWidth;

                        // 次の行の行頭になる
                        originX = 0;

                        // yは親の分移動する
                        originY -= rectHeight;

                        var newTailTextElementRectTrans = newTextElement.GetComponent<RectTransform>();
                        newTailTextElementRectTrans.anchoredPosition = new Vector2(originX, originY);

                        // 残りのデータをテキスト特有の継続したコンテンツ扱いする。
                        continueContent = true;

                        // 生成したコンテンツを次の行の要素へと追加する
                        lineContents.Add(newTailTextElementRectTrans);

                        textElement = newTextElement;
                        contentText = lastLineText;
                        goto NextLine;
                    }
                case TextLayoutStatus.NotHeadAndOutOfView:
                    {
                        // 次の行にコンテンツを置き、継続する

                        // 現在最後の追加要素である自分自身を取り出し、ここまでの行の要素を整列させる。
                        lineContents.RemoveAt(lineContents.Count - 1);
                        ElementLayoutFunctions.LineFeed(ref originX, ref originY, currentLineMaxHeight, ref currentLineMaxHeight, ref lineContents);

                        // レイアウト対象のビューサイズを新しい行のものとして更新する
                        restWidth = viewWidth;
                        lineContents.Add(textComponent.rectTransform);
                        goto NextLine;
                    }
            }
        }

        /*
            絵文字が入った文字列をintenalな矩形と文字としてレイアウトする。ここでやることで、TM Proによるレイアウトの破綻を避ける。
        */
        private static void LayoutContentWithEmoji<T>(T textElement, string contentText, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents) where T : LTElement, ILayoutableText
        {
            /*
                絵文字が含まれている文字列を、絵文字と矩形に分解、再構成を行う。絵文字を単に画像が入る箱としてRectLayoutに放り込む。というのがいいのか、それとも独自にinternalを定義した方がいいのか。
                後者だなー、EmojiRectみたいなのを用意しよう。

                自分自身を書き換えて、一連のコマンドを実行するようにする。
                文字がどう始まるかも含めて、今足されているlinedからは一度離反する。その上で一つ目のコンテンツを追加する。
            */
            var elementsWithEmoji = CollectEmojiAndMarkAndTextElement(textElement, contentText);

            for (var i = 0; i < elementsWithEmoji.Count; i++)
            {
                var element = elementsWithEmoji[i];
                var rectTrans = element.GetComponent<RectTransform>();
                restWidth = viewWidth - originX;
                lineContents.Add(rectTrans);

                if (element is InternalEmojiRect)
                {
                    // emojiRectが入っている
                    var internalRectElement = (InternalEmojiRect)element;
                    EmojiRectLayout(internalRectElement, rectTrans, viewWidth, ref originX, ref originY, ref restWidth, ref currentLineMaxHeight, ref lineContents);
                    continue;
                }

                // ここに来るということは、T型が入っている。
                var internalTextElement = (T)element;
                var internalContentText = internalTextElement.Text();

                TextLayout(internalTextElement, internalContentText, rectTrans, viewWidth, ref originX, ref originY, ref restWidth, ref currentLineMaxHeight, ref lineContents);
            }
        }

        public static bool IsDetectEmojiAndMarkAndTextExist(string contentText)
        {
            for (var i = 0; i < contentText.Length; i++)
            {
                var firstChar = contentText[i];

                // \u26A1
                var isSymbol = Char.IsSymbol(firstChar);
                if (isSymbol)
                {
                    return true;
                }

                // \U0001F971
                var isSurrogate = Char.IsSurrogate(firstChar);
                if (isSurrogate)
                {
                    if (i == contentText.Length - 1)
                    {
                        continue;
                    }

                    var nextChar = contentText[i + 1];
                    var isSurrogatePair = Char.IsSurrogatePair(firstChar, nextChar);

                    if (isSurrogatePair)
                    {
                        return true;
                    }

                    // 後続の文字がsurrogateではなかった。
                    continue;
                }
            }

            return false;
        }

        private static List<LTElement> CollectEmojiAndMarkAndTextElement<T>(T textElement, string contentText) where T : LTElement, ILayoutableText
        {
            var elementsWithEmoji = new List<LTElement>();

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
                    var emojiElement = InternalEmojiRect.GO(textElement, new Char[] { firstChar }).GetComponent<InternalEmojiRect>();
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
                        var emojiElement = InternalEmojiRect.GO(textElement, new Char[] { firstChar, nextChar }).GetComponent<InternalEmojiRect>();
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


        private static void EmojiRectLayout(InternalEmojiRect rectElement, RectTransform transform, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents)
        {
            var rectSize = rectElement.RectSize();
            RectLayout(rectElement, transform, rectSize, viewWidth, ref originX, ref originY, ref restWidth, ref currentLineMaxHeight, ref lineContents);
        }

        public static void RectLayout(LTElement rectElement, RectTransform transform, Vector2 rectSize, float viewWidth, ref float originX, ref float originY, ref float restWidth, ref float currentLineMaxHeight, ref List<RectTransform> lineContents)
        {
            Debug.Assert(transform.pivot.x == 0 && transform.pivot.y == 1 && transform.anchorMin.x == 0 && transform.anchorMin.y == 1 && transform.anchorMax.x == 0 && transform.anchorMax.y == 1, "rectTransform for LayouTaro should set pivot to 0,1 and anchorMin 0,1 anchorMax 0,1.");
            if (restWidth < rectSize.x)// 同じ列にレイアウトできないので次の列に行く。
            {
                // 現在最後の追加要素である自分自身を取り出し、整列させる。
                lineContents.RemoveAt(lineContents.Count - 1);
                ElementLayoutFunctions.LineFeed(ref originX, ref originY, currentLineMaxHeight, ref currentLineMaxHeight, ref lineContents);
                lineContents.Add(transform);

                // 位置をセット
                transform.anchoredPosition = new Vector2(originX, originY);
            }
            else
            {
                // 位置をセット
                transform.anchoredPosition = new Vector2(originX, originY);
            }

            // ジャストで埋まったら、次の行を作成する。
            if (restWidth == rectSize.x)
            {
                ElementLayoutFunctions.LineFeed(ref originX, ref originY, transform.sizeDelta.y, ref currentLineMaxHeight, ref lineContents);
                return;
            }

            ElementLayoutFunctions.ContinueLine(ref originX, rectSize.x, transform.sizeDelta.y, ref currentLineMaxHeight);
        }

        public static void LayoutLastLine(ref float originY, float currentLineMaxHeight, ref List<RectTransform> lineContents)
        {
            // レイアウト終了後、最後の列の要素を並べる。
            foreach (var element in lineContents)
            {
                var rectTrans = element.GetComponent<RectTransform>();

                var elementHeight = rectTrans.sizeDelta.y;
                var parent = rectTrans.parent;
                if (parent == null)
                {
                    continue;
                }

                var isParentRoot = parent.GetComponent<LTElement>() is LTRootElement;
                if (isParentRoot)
                {
                    rectTrans.anchoredPosition = new Vector2(
                        rectTrans.anchoredPosition.x,
                        originY - (currentLineMaxHeight - elementHeight) / 2
                    );
                }
                else
                {
                    // 親がRootElementではない場合、なんらかの子要素なので、行の高さは合うが、上位の単位であるoriginYとの相性が悪すぎる。なので、独自の計算系で合わせる。
                    rectTrans.anchoredPosition = new Vector2(
                        rectTrans.anchoredPosition.x,
                        rectTrans.anchoredPosition.y -// 子要素は親からの絶対的な距離を独自に保持しているので、それ + 行全体を整頓した際の高さの隙間、という計算を行う。
                            (
                                currentLineMaxHeight// この行全体の高さからこの要素の高さを引いて/2して、「要素の上の方の隙間高さ」を手に入れる
                                - elementHeight
                            )
                            / 2
                        );
                }
            }

            // 最終的にyを更新する。
            originY -= currentLineMaxHeight;
        }
    }
}