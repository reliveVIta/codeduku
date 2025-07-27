using SkiaSharp;

namespace CodeDuku
{
    /// <summary>
    /// Represents a single cell in the crossword grid, including its string, color, and position.
    /// </summary>
    struct CellData
    {
        public string Letter;         // The string stored in the cell.
        public SKColor Background;    // The background color of the cell (used for canvas).
        public string ColorName;      // The name of the color used (for tracking purposes).
        public int Row;               // row index of letter.
        public int Col;               // col index of letter.
        public int PhraseIndex;       // Index in inputNames for the string the cell is part of
        public bool DrawRight;        // Direction: true for horizontal, false for vertical
        public int BaseRow;           // Row index of the base (start) of the phrase
        public int BaseCol;           // Col index of the base (start) of the phrase
        public bool IsOverlap;        // True if this cell contains an overlapping letter from multiple words

        public CellData(string letter = "", SKColor? background = null, string colorName = "white", int row = 0, int col = 0, int phraseIndex = 0, bool drawRight = true, int baseRow = 0, int baseCol = 0, bool isOverlap = false)
        {
            Letter = letter;
            Background = background ?? SKColors.White;
            ColorName = colorName;
            Row = row;
            Col = col;
            PhraseIndex = phraseIndex;
            DrawRight = drawRight;
            BaseRow = baseRow;
            BaseCol = baseCol;
            IsOverlap = isOverlap;
        }
    }
    
    /// <summary>
    /// Represents a word to be drawn on the grid, including its content, position, and orientation.
    /// Created to simplify values being passed and returned by functions
    /// </summary>
    struct PhraseStruct
    {
        public string Phrase;    // The string stored in the cell.
        public int Row;          // row index of phrase.
        public int Col;          // col index of phrase.
        public bool DrawRight;  // Direction: true for horizontal, false for vertical.

        public PhraseStruct(string phrase = "", int row = 0, int col = 0, bool drawRight = true)
        {
            Phrase = phrase;
            Row = row;
            Col = col;
            DrawRight = drawRight;
        }
    }
    
    /// <summary>
    /// Represents a hint placed on the crossword grid, including its position, color, neighbor positions, and hint value.
    /// </summary>
    struct PlacedHint
    {
        public int Row;
        public int Col;
        public string Color;
        public List<(int row, int col)> NeighborPositions;
        public string HintValue;
        public int PhraseIndex;
        public string Difficulty;
        public PlacedHint(int row, int col, string color, List<(int, int)> neighborPositions, string hintValue, int phraseIndex, string difficulty)
        {
            Row = row;
            Col = col;
            Color = color;
            NeighborPositions = neighborPositions;
            HintValue = hintValue;
            PhraseIndex = phraseIndex;
            Difficulty = difficulty;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<char, int> charToIntMapping = new();
            Dictionary<int, char> intToCharMapping = new();
            Dictionary<string, object> languageDicts = new();
            List<string> inputNames = GetNames(@"../../../../../input_names.txt");
            Dictionary<string, SKColor> colors = new();
            List<List<CellData>> cellData = new();

            languageDicts["charToInt"] = charToIntMapping;
            languageDicts["intToChar"] = intToCharMapping;

            int nrows = 20, ncols = 20, cellSize = 50;
            int numWords = 22; // Number of words to place in the puzzle
            int numHints = 10;
            string filename = "crossword";
            string extension = ".png";

            // key is difficulty level, value is weight
            // difficulty for each hint is randomly selected based on these weights
            var difficultyWeights = new Dictionary<string, float>
            {
                { "beginner", 0.1f },     // 1 neighbor
                { "novice", 0.3f },       // 2 neighbors
                { "intermediate", 0.2f }, // 3 neighbors
                { "expert", 0.15f },       // 4 neighbors
                { "master", 0.15f },       // 5 neighbors
                { "legendary", 0.1f }     // 6 neighbors
            };

            using var bitmap = new SKBitmap(ncols * cellSize, nrows * cellSize);
            using var canvas = new SKCanvas(bitmap);

            InitializeGrid(ref cellData, nrows, ncols); //proper error handling is needed
            InitializeMappings(intToCharMapping, charToIntMapping);
            DefineColors(colors);
            InitializeCanvas(nrows, ncols, canvas, cellSize);

            CreatePuzzle(ref cellData, inputNames, canvas, languageDicts, colors, cellSize, numWords: numWords, numHints: numHints, difficultyWeights: difficultyWeights, bitmap);
            ExportPuzzle(bitmap, filename: filename + extension);

            ClearGridLetters(ref cellData, canvas, cellSize);
            ExportPuzzle(bitmap, filename: filename + "_blank" + extension);
        }

        /// <summary>
        /// Generates a crossword puzzle by placing words and Hints on the grid and drawing them on the canvas.
        /// </summary>
        /// <param name="cellData">The crossword grid to populate.</param>
        /// <param name="inputNames">List of possible input words.</param>
        /// <param name="canvas">The canvas to draw the puzzle on.</param>
        /// <param name="languageDicts">Dictionaries used for encoding letters (e.g., char-to-int mapping).</param>
        /// <param name="colors">Mapping of color names to SKColor values.</param>
        /// <param name="cellSize">Size in pixels of each grid cell.</param>
        /// <param name="numWords">Number of words to place in the puzzle.</param>
        /// <param name="numHints">Number of Hints to generate.</param>
        /// <param name="difficulty">String indicating puzzle difficulty level.</param>
        /// <returns>The updated crossword grid with placed words and hints.</returns>
        private static List<List<CellData>> CreatePuzzle(
            ref List<List<CellData>> cellData,
            List<string> inputNames,
            SKCanvas canvas,
            Dictionary<string, object> languageDicts,
            Dictionary<string, SKColor> colors,
            int cellSize,
            int numWords,
            int numHints,
            Dictionary<string, float> difficultyWeights,
            SKBitmap bitmap)
        {
            if (numWords <= 0)
                throw new ArgumentException("numWords must be greater than 0");
            if (numHints <= 0)
                throw new ArgumentException("numHints must be greater than 0");
            if (difficultyWeights.Sum(pair => pair.Value) != 1.0f)
            {
                throw new ArgumentException("Difficulty weights must sum to 1.0");
            }

            Random rand = new Random();
            List<string> weightedPool = new();

            foreach (var pair in difficultyWeights)
            {
                int count = (int)(pair.Value * 10);
                for (int i = 0; i < count; i++)
                {
                    weightedPool.Add(pair.Key);
                }
            }

            List<PhraseStruct> inputsAdded = new List<PhraseStruct>();
            List<PlacedHint> HintsAdded = new List<PlacedHint>();
            bool puzzleIsUniquelySolvable = true;

            inputsAdded = DrawSeedWord(ref cellData, inputNames, canvas, cellSize);

            for (int i = 0; i < numWords - 1; i++)
            {
                DrawRandomWord(ref cellData, inputNames, ref inputsAdded, canvas, cellSize);
            }

            for (int i = 0; i < numHints; i++)
            {
                bool uniquelySolvable = false; // reset for each Hint. unnecessary.
                // valid difficulties: "beginner", "novice", "intermediate", "expert", "master", "legendary"
                string difficulty = weightedPool.OrderBy(_ => rand.Next()).ToList()[0];
                Console.WriteLine($"Selected difficulty: {difficulty}");
                
                bool HintCreated = false;
                string currentDifficulty = difficulty;
                
                while (!HintCreated)
                {
                    HintCreated = CreateHint(cellData, canvas, colors, cellSize, languageDicts, ref HintsAdded, numHints, ref uniquelySolvable, inputNames, currentDifficulty);
                    
                    if (!HintCreated)
                    {
                        Console.WriteLine($"Failed to create Hint with difficulty '{currentDifficulty}'. Stepping down difficulty and retrying...");
                        string steppedDownDifficulty = StepDownDifficulty(currentDifficulty);
                        if (steppedDownDifficulty != currentDifficulty)
                        {
                            Console.WriteLine($"Retrying with difficulty '{steppedDownDifficulty}'");
                            currentDifficulty = steppedDownDifficulty;
                        }
                        else
                        {
                            Console.WriteLine("Already at minimum difficulty level, skipping this hint");
                            break;
                        }
                    }
                }
                puzzleIsUniquelySolvable = puzzleIsUniquelySolvable && uniquelySolvable;
            }

            UpdateFullCanvas(canvas, cellData, cellSize);
            ExportPuzzle(bitmap, filename: "crossword_unique_step_0.png");

            // Pre-filter words by length for faster lookup - created once outside the loop
            var wordsByLength = new Dictionary<int, List<int>>();
            for (int i = 0; i < inputNames.Count; i++)
            {
                int len = inputNames[i].Length;
                if (!wordsByLength.ContainsKey(len))
                    wordsByLength[len] = new List<int>();
                wordsByLength[len].Add(i);
            }

            // Pre-build slots once outside the loop to avoid rebuilding on every makePuzzleUnique call
            List<(int row, int col, bool drawRight, int length, int phraseIndex)> slots = new();
            var processedPhrases = new HashSet<int>();

            // Loop through cellData to find unique phrases and build slots
            int nrows = cellData.Count;
            int ncols = cellData[0].Count;
            for (int r = 0; r < nrows; r++)
            {
                for (int c = 0; c < ncols; c++)
                {
                    var cell = cellData[r][c];
                    
                    // Check if this cell contains a letter (not hint) and we haven't processed this phrase yet
                    if (!string.IsNullOrEmpty(cell.Letter) && 
                        !cell.Letter.StartsWith("=") && 
                        !processedPhrases.Contains(cell.PhraseIndex))
                    {
                        // Add the slot using BaseRow, BaseCol, DrawRight, and phrase index for length lookup
                        slots.Add((cell.BaseRow, cell.BaseCol, cell.DrawRight, inputNames[cell.PhraseIndex].Length, cell.PhraseIndex));
                        processedPhrases.Add(cell.PhraseIndex);
                    }
                }
            }
            Console.WriteLine($"[CreatePuzzle] Pre-built {slots.Count} slots");

            // Pre-calculate all coordinate positions for each slot to avoid repeated calculations
            var slotPositions = new Dictionary<int, (int r, int c)[]>();
            for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
            {
                var slot = slots[slotIndex];
                var positions = new (int r, int c)[slot.length];
                for (int i = 0; i < slot.length; i++)
                {
                    positions[i] = (slot.row + (slot.drawRight ? 0 : i), slot.col + (slot.drawRight ? i : 0));
                }
                slotPositions[slotIndex] = positions;
            }

            puzzleIsUniquelySolvable = false;
            int j = 1;
            while (!puzzleIsUniquelySolvable)
            {
                var returnValue = makePuzzleUnique(ref cellData, inputNames, canvas, cellSize, HintsAdded, wordsByLength, languageDicts, slots, slotPositions);
                puzzleIsUniquelySolvable = returnValue.isUnique;
                var bestIndex = returnValue.bestHint.bestIndex;
                var bestColor = returnValue.bestHint.color;
                if (!puzzleIsUniquelySolvable)
                {
                    ColorHint(cellData, canvas, colors, bestColor, new CellData(row: bestIndex.row, col: bestIndex.col), cellSize, languageDicts, ref HintsAdded, inputNames, "beginner");
                    UpdateFullCanvas(canvas, cellData, cellSize);
                    Console.WriteLine($"[Solver] Exported alternate solution to crossword_unique_step_{j}.png");
                    ExportPuzzle(bitmap, filename: $"crossword_unique_step_{j}.png");
                    j++;
                }
            }

            UpdateFullCanvas(canvas, cellData, cellSize);

            string solutionStatus = puzzleIsUniquelySolvable ? "unique" : "non-unique";
            Console.WriteLine($"[Result] Final puzzle is {solutionStatus}");

            return cellData;
        }

        /// <summary>
        /// Steps down the difficulty level to a lower/easier level when hint creation fails.
        /// Used for fallback when no valid hint positions are found for the current difficulty.
        /// </summary>
        /// <param name="currentDifficulty">The current difficulty level that failed.</param>
        /// <returns>
        /// A string representing the next lower difficulty level, or the same difficulty if already at minimum.
        /// Difficulty hierarchy (hardest to easiest): legendary -> master -> expert -> intermediate -> novice -> beginner
        /// </returns>
        private static string StepDownDifficulty(string currentDifficulty)
        {
            // Define the difficulty hierarchy from hardest to easiest
            // Each level corresponds to the number of neighbor cells required:
            // legendary: 6 neighbors, master: 5 neighbors, expert: 4 neighbors, 
            // intermediate: 3 neighbors, novice: 2 neighbors, beginner: 1 neighbor
            return currentDifficulty.ToLower() switch
            {
                "legendary" => "master",
                "master" => "expert", 
                "expert" => "intermediate",
                "intermediate" => "novice",
                "novice" => "beginner",
                "beginner" => "beginner", // Already at minimum difficulty
                _ => "beginner" // Default fallback for unknown difficulties
            };
        }

        /// <summary>
        /// Attempts to find a valid position and draw a randomly selected word from the input list onto the crossword grid.
        /// used for setting the "seed" of the crossword
        /// </summary>
        /// <param name="cellData">2D list representing the crossword grid.</param>
        /// <param name="inputNames">List of all input words to possibly draw.</param>
        /// <param name="inputsAdded">Reference list of words that have already been placed.</param>
        /// <returns>
        /// Tuple:
        /// - success (bool): True if a word was successfully placed.
        /// - phraseInfo (phrase_struct): The placement information for the selected word.
        /// </returns>
        private static bool DrawRandomWord(
            ref List<List<CellData>> cellData,
            List<string> inputNames,
            ref List<PhraseStruct> inputsAdded,
            SKCanvas canvas,
            int cellSize)
        {
            Random rand = new Random();
            var inputsAddedCopy = inputsAdded; // Able to be used with a lambda expression
            List<bool> drawRightValues = new List<bool>() { true, false };
            drawRightValues = drawRightValues.OrderBy(_ => rand.Next()).ToList();


            // Filter out words that have already been added and shuffle the remaining candidates
            var candidates = inputNames
                .Where(name => !inputsAddedCopy.Any(p => p.Phrase == name))
                .OrderBy(_ => rand.Next())
                .ToList();

            int nrows = cellData.Count;
            int ncols = cellData[0].Count;

            foreach (var randomInput in candidates)
            {
                drawRightValues = drawRightValues.OrderBy(_ => rand.Next()).ToList();
                foreach (bool drawRight in drawRightValues)
                {
                    int maxRow = drawRight ? nrows - 1 : nrows - randomInput.Length + 1;
                    int maxCol = drawRight ? ncols - randomInput.Length + 1 : ncols - 1;
                    for (int row = 0; row < maxRow; row++)
                    {
                        for (int col = 0; col < maxCol; col++)
                        {
                            var phraseInfo = new PhraseStruct(randomInput, row, col, drawRight);
                            if (!DrawStringCheck(cellData, phraseInfo))
                                continue;

                            // Ensure there is at least one overlapping letter
                            // Ensure there is no consecutive overlapping letters
                            bool hasOverlap = false;
                            int consecutiveOverlap = 0;
                            bool currentOverlap = false;
                            for (int j = 0; j < randomInput.Length; j++)
                            {
                                currentOverlap = false;
                                int r = row + (drawRight ? 0 : j);
                                int c = col + (drawRight ? j : 0);
                                if (!string.IsNullOrEmpty(cellData[r][c].Letter) &&
                                    cellData[r][c].Letter.ToLower() == randomInput[j].ToString().ToLower())
                                {
                                    hasOverlap = true;
                                    currentOverlap = true;
                                    consecutiveOverlap++;
                                }
                                if (consecutiveOverlap > 1)
                                    break;
                                else if (!currentOverlap)
                                    consecutiveOverlap = Math.Max(0, consecutiveOverlap - 1);
                            }

                            if (!hasOverlap || consecutiveOverlap > 1)
                            {
                                continue;
                            }
                            // Check for visual spacing conflicts (letters placed directly parallel)
                            bool hasParallelConflict = false;
                            for (int j = 0; j < randomInput.Length; j++)
                            {
                                int r = row + (drawRight ? 0 : j);
                                int c = col + (drawRight ? j : 0);

                                if (!string.IsNullOrEmpty(cellData[r][c].Letter) &&
                                    cellData[r][c].Letter == randomInput[j].ToString())
                                {
                                    continue;
                                }

                                if (drawRight)
                                {
                                    if ((row > 0 && !string.IsNullOrEmpty(cellData[row - 1][c].Letter)) ||
                                        (row < nrows - 1 && !string.IsNullOrEmpty(cellData[row + 1][c].Letter)))
                                    {
                                        hasParallelConflict = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    if ((col > 0 && !string.IsNullOrEmpty(cellData[r][col - 1].Letter)) ||
                                        (col < ncols - 1 && !string.IsNullOrEmpty(cellData[r][col + 1].Letter)))
                                    {
                                        hasParallelConflict = true;
                                        break;
                                    }
                                }
                            }

                            if (hasParallelConflict) continue;

                            // Check for cells before and after the word
                            int beforeRow = row - (drawRight ? 0 : 1);
                            int beforeCol = col - (drawRight ? 1 : 0);
                            int afterRow = row + (drawRight ? 0 : randomInput.Length);
                            int afterCol = col + (drawRight ? randomInput.Length : 0);
                            bool beforeInBounds = beforeRow >= 0 && beforeCol >= 0 && beforeRow < nrows && beforeCol < ncols;
                            bool afterInBounds = afterRow >= 0 && afterCol >= 0 && afterRow < nrows && afterCol < ncols;
                            if ((beforeInBounds && !string.IsNullOrEmpty(cellData[beforeRow][beforeCol].Letter)) ||
                                (afterInBounds && !string.IsNullOrEmpty(cellData[afterRow][afterCol].Letter)))
                            {
                                continue;
                            }

                            inputsAdded.Add(phraseInfo);
                            DrawString(ref cellData, phraseInfo, canvas, cellSize, inputNames);
                            return true;
                        }
                    }
                }
            }
            return false; // No valid position found
        }

        /// <summary>
        /// Attempts to place a randomly selected word on the grid in a valid location and orientation.
        /// Must overlap an existing word (key constraint)
        /// </summary>
        /// <param name="cellData">The crossword grid.</param>
        /// <param name="inputNames">List of available words to choose from.</param>
        /// <returns>
        /// A tuple containing:
        /// - success (bool): True if a word was successfully placed.
        /// - phraseInfo (phrase_struct): The placement information.
        /// - inputsAdded (List string): List containing the added word if placement was successful.
        /// </returns>
        private static List<PhraseStruct> DrawSeedWord(
            ref List<List<CellData>> cellData,
            List<string> inputNames,
            SKCanvas canvas,
            int cellSize)
        {
            Random rand = new();
            List<bool> options = new() { true, false };

            bool drawRight = options[rand.Next(2)];

            int nrows = cellData.Count;
            int ncols = cellData[0].Count;

            List<string> filteredInputNames = drawRight
                ? inputNames.Where(name => name.Length <= ncols).ToList()
                : inputNames.Where(name => name.Length <= nrows).ToList();

            if (filteredInputNames.Count == 0)
                return new List<PhraseStruct>();

            string randomInput = filteredInputNames[rand.Next(filteredInputNames.Count)];

            int maxRow = drawRight ? nrows - 1 : nrows - randomInput.Length;
            int maxCol = drawRight ? ncols - randomInput.Length : ncols - 1;

            var phraseInfo = new PhraseStruct(randomInput, rand.Next(0, maxRow), rand.Next(0, maxCol), drawRight);

            if (DrawStringCheck(cellData, phraseInfo))
            {
                DrawString(ref cellData, phraseInfo, canvas, cellSize, inputNames);
                return new List<PhraseStruct> { phraseInfo };
            }

            return new List<PhraseStruct>();
        }

        /// <summary>
        /// Computes a hint string based on the sum of encoded character values at specified grid positions.
        /// </summary>
        /// <param name="pickedBorderIndex">List of (row, column) positions to include in the calculation.</param>
        /// <param name="languageDicts">Dictionary containing character-to-integer and integer-to-character mappings.</param>
        /// <param name="cellData">The crossword grid containing letters used for calculation.</param>
        /// <returns>
        /// A string starting with '=' followed by a character representing the encoded sum modulo 62.
        /// </returns>
        private static string CalculateHint(List<(int, int)> pickedBorderIndex, Dictionary<string, object> languageDicts, List<List<CellData>> cellData)
        {
            var charToInt = (Dictionary<char, int>)languageDicts["charToInt"];
            var intToChar = (Dictionary<int, char>)languageDicts["intToChar"];

            int sum = 0;

            foreach (var (r, c) in pickedBorderIndex)
            {
                string letter = cellData[r][c].Letter;

                if (!string.IsNullOrEmpty(letter))
                {
                    char ch = letter[0]; // assumes letter is a single-character string
                    if (charToInt.ContainsKey(ch))
                    {
                        sum += charToInt[ch];
                    }
                }
            }

            int modValue = sum % 62;

            return "=" + intToChar[modValue];
        }

        /// <summary>
        /// Populates the provided dictionary with named SKColor values used in the crossword puzzle.
        /// </summary>
        /// <param name="colors">A dictionary to store color names and their corresponding SKColor values.</param>
        private static void DefineColors(Dictionary<string, SKColor> colors)
        {
            colors.Add("red", new SKColor(243, 174, 172));
            colors.Add("blue", new SKColor(170, 170, 249));
            colors.Add("green", new SKColor(189, 253, 178));
            colors.Add("dark_red", new SKColor(218, 100, 95));
            colors.Add("dark_blue", new SKColor(90, 90, 227));
            colors.Add("dark_green", new SKColor(132, 232, 110));
            colors.Add("red_blue", new SKColor(212, 168, 212));
            colors.Add("red_green", new SKColor(212, 212, 168));
            ///colors.Add("blue_green", new SKColor(168, 212, 212));
            colors.Add("blue_green", new SKColor(240, 199, 183));
            colors.Add("red_blue_green", new SKColor(197, 197, 197));
        }

        static void InitializeCanvas(int nrows, int ncols, SKCanvas canvas, int cellSize)
        {
            var borderPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsStroke = true };
            canvas.Clear(SKColors.White);

            for (int i = 0; i < nrows * ncols; i++)
            {
                int row = i / ncols;
                int col = i % ncols;
                var rect = new SKRect(col * cellSize, row * cellSize, (col + 1) * cellSize, (row + 1) * cellSize);
                canvas.DrawRect(rect, borderPaint);
            }
        }

        /// <summary>
        /// Loads and returns a list of words from a text file, replacing 'ä' with 'a'.
        /// </summary>
        /// <param name="fileName">Path to the input text file.</param>
        /// <returns>List of cleaned words.</returns>
        private static List<string> GetNames(string fileName)
        {
            List<string> outputList = File.ReadAllLines(fileName).ToList();
            outputList = outputList.Select(s => s.Replace('ä', 'a')).ToList(); // one string in the file has this
            return outputList;
        }

        /// <summary>
        /// Initializes two maps containing conversion info to and from codeduku ascii.
        /// </summary>
        /// <param name="intToCharMapping">The map that converts ints to their equivalent char representation.</param>
        /// <param name="charToIntMapping">The map that converts chars to their equivalent int representation.</param>
        /// <returns>void.</returns>
        private static void InitializeMappings(Dictionary<int, char> intToCharMapping, Dictionary<char, int> charToIntMapping)
        {
            int total = 0;
            for (int i = 48; i <= 57; i++) // 0 - 9
            {
                intToCharMapping.Add(total + (i - 48), (char)i);
                charToIntMapping.Add((char)i, total + (i - 48));
                // Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 48)));
            }
            total += (57 - 48) + 1;
            for (int i = 97; i <= 122; i++) // a - z
            {
                intToCharMapping.Add(total + (i - 97), (char)i);
                charToIntMapping.Add((char)i, total + (i - 97));
                // Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 97)));
            }
            total += (122 - 97) + 1;
            for (int i = 65; i <= 90; i++) // A - Z
            {
                intToCharMapping.Add(total + (i - 65), (char)i);
                charToIntMapping.Add((char)i, total + (i - 65));
                // Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 65)));
            }
        }

        /// <summary>
        /// Updates the entire canvas by redrawing all cells in the grid.
        /// </summary>
        /// <param name="canvas">The SKCanvas to update.</param>
        /// <param name="cellData">The 2D grid of CellData representing the puzzle.</param>
        /// <param name="cellSize">The size of each cell in pixels.</param>
        private static void UpdateFullCanvas(SKCanvas canvas, List<List<CellData>> cellData, int cellSize)
        {
            for (int r = 0; r < cellData.Count; r++)
            {
                for (int c = 0; c < cellData[0].Count; c++)
                {
                    ModifyCanvas(canvas, cellData, r, c, cellSize);
                }
            }
        }

        /// <summary>
        /// Updates the canvas to reflect the background and letter of a specific cell.
        /// </summary>
        /// <param name="canvas">The SKCanvas to draw on.</param>
        /// <param name="cellData">2D grid of CellData representing the puzzle.</param>
        /// <param name="row">Row index of the cell to update.</param>
        /// <param name="col">Column index of the cell to update.</param>
        /// <param name="cellSize">Pixel size of each cell.</param>
        /// <returns>True if the cell was drawn successfully; otherwise, false.</returns>
        private static bool ModifyCanvas(SKCanvas canvas, List<List<CellData>> cellData, int row, int col, int cellSize)
        {
            if (row < 0 || col < 0) return false;

            // Check bounds based on list dimensions
            if (row >= cellData.Count || col >= cellData[0].Count) return false;

            int textSize = 18; // Default text size

            try
            {
                var cell = cellData[row][col];

                // Internal paint setup
                var borderPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    StrokeWidth = 2,
                    IsStroke = true
                };

                var textPaint = new SKPaint
                {
                    Color = SKColors.Black,
                    IsAntialias = true,
                };
                var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), textSize);

                // Cell position
                int x = col * cellSize;
                int y = row * cellSize;
                var rect = new SKRect(x, y, x + cellSize, y + cellSize);

                // Draw background and border
                canvas.DrawRect(rect, new SKPaint { Color = cell.Background });
                canvas.DrawRect(rect, borderPaint);

                // Draw letter if present
                if (!string.IsNullOrEmpty(cell.Letter))
                {
                    float textX = x + cellSize / 2f;
                    float textY = y + cellSize / 2f + textSize / 3f;
                    canvas.DrawText(cell.Letter, textX, textY, SKTextAlign.Center, font, textPaint);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Draws a word onto the grid and canvas, setting letters and background color.
        /// Capital letters take precedence if present.
        /// </summary>
        /// <param name="cellData">The grid to update.</param>
        /// <param name="phraseInfo">Word and position/orientation to draw.</param>
        /// <param name="canvas">The canvas to render on.</param>
        /// <param name="cellSize">Pixel size of each cell.</param>
        private static void DrawString(ref List<List<CellData>> cellData, PhraseStruct phraseInfo, SKCanvas canvas, int cellSize, List<string> inputNames)
        {
            if (!DrawStringCheck(cellData, phraseInfo))
            {
                Console.WriteLine("Cannot draw string: does not fit or cells occupied.");
                return;
            }

            // Find the phrase index in inputNames
            int phraseIndex = inputNames.FindIndex(name => name == phraseInfo.Phrase);
            for (int i = 0; i < phraseInfo.Phrase.Length; i++)
            {
                int r = phraseInfo.Row + (phraseInfo.DrawRight ? 0 : i);
                int c = phraseInfo.Col + (phraseInfo.DrawRight ? i : 0);

                string newChar = phraseInfo.Phrase[i].ToString();
                string existingChar = cellData[r][c].Letter;

                // Check if this is an overlapping letter (cell already has a letter)
                bool isOverlapCell = !string.IsNullOrEmpty(existingChar);

                // Determine if the resulting character should be uppercase
                bool shouldBeUpper =
                    (!string.IsNullOrEmpty(newChar) && char.IsUpper(newChar[0])) ||
                    (!string.IsNullOrEmpty(existingChar) && char.IsUpper(existingChar[0]));
                string finalChar = shouldBeUpper ? newChar.ToUpper() : newChar.ToLower();


                var cell = cellData[r][c]; // structs are passed by value so need to create a copy to modify
                cell.Letter = finalChar;
                cell.Background = SKColors.LightGray;
                cell.ColorName = "lightgray";
                cell.PhraseIndex = phraseIndex;
                cell.DrawRight = phraseInfo.DrawRight;
                cell.BaseRow = phraseInfo.Row;
                cell.BaseCol = phraseInfo.Col;
                cell.IsOverlap = isOverlapCell; // Set to true if there was already a letter in this cell
                cellData[r][c] = cell;

                ModifyCanvas(canvas, cellData, r, c, cellSize);
            }
        }

        /// <summary>
        /// Checks if a word can be placed on the grid without conflicts.
        /// </summary>
        /// <param name="cellData">The grid to check against.</param>
        /// <param name="phraseInfo">Word and position/orientation to validate.</param>
        /// <returns>True if the word can be placed; otherwise, false.</returns>
        private static bool DrawStringCheck(List<List<CellData>> cellData, PhraseStruct phraseInfo)
        {
            // Check bounds
            if (phraseInfo.DrawRight && phraseInfo.Col + phraseInfo.Phrase.Length > cellData[0].Count) return false;
            if (!phraseInfo.DrawRight && phraseInfo.Row + phraseInfo.Phrase.Length > cellData.Count) return false;

            for (int i = 0; i < phraseInfo.Phrase.Length; i++)
            {
                int r = phraseInfo.Row + (phraseInfo.DrawRight ? 0 : i);
                int c = phraseInfo.Col + (phraseInfo.DrawRight ? i : 0);

                if (!string.IsNullOrEmpty(cellData[r][c].Letter) &&
                    cellData[r][c].Letter.ToLower() != phraseInfo.Phrase[i].ToString().ToLower())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Initializes the crossword grid by creating a 2D list of CellData with specified rows and columns.
        /// Each cell's row and column indices are set accordingly.
        /// </summary>
        /// <param name="cellData">The 2D list of CellData to initialize (passed by reference).</param>
        /// <param name="nrows">Number of rows in the grid.</param>
        /// <param name="ncols">Number of columns in the grid.</param>
        /// <returns>True if the grid was initialized successfully; otherwise, false.</returns>
        private static bool InitializeGrid(ref List<List<CellData>> cellData, int nrows, int ncols)
        {
            try
            {
                cellData.Clear(); // just in case
                cellData = new List<List<CellData>>();
                for (int r = 0; r < nrows; r++)
                {
                    var row = new List<CellData>();
                    for (int c = 0; c < ncols; c++)
                    {
                        row.Add(new CellData(row: r, col: c));
                    }
                    cellData.Add(row);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing grid: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the provided SKBitmap as a PNG image file with the specified filename.
        /// </summary>
        /// <param name="bitmap">The SKBitmap image to export.</param>
        /// <param name="filename">The name of the output PNG file.</param>
        private static void ExportPuzzle(SKBitmap bitmap, string filename)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(filename);
            data.SaveTo(stream);
            Console.WriteLine("Crossword saved as " + filename);
        }

        /// <summary>
        /// Retrieves the diagonal and non-diagonal (cross) neighbors of a given cell in the grid.
        /// For neighbors out of bounds, returns default CellData instances.
        /// </summary>
        /// <param name="cellData">The 2D list of CellData representing the grid.</param>
        /// <param name="cell">The CellData instance whose neighbors are to be found (uses its row and col properties).</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item><description>List of diagonal neighbors (diag_neighbors)</description></item>
        /// <item><description>List of non-diagonal cross neighbors (cross_neighbors)</description></item>
        /// </list>
        /// </returns>
        private static (List<CellData> diag_neighbors, List<CellData> cross_neighbors) GetNeighbors(List<List<CellData>> cellData, CellData cell, bool isAlternate = false)
        {
            // todo: modify to do somthing different if the cell passed in is default
            var diagNeighbors = new List<CellData>();
            var crossNeighbors = new List<CellData>();
            List<string> validColors = new List<string> { "lightgray", "red", "blue", "green", "red_blue", "red_green", "blue_green", "red_blue_green" };

            // All 8 neighbor offsets: diagonals and crosses
            (int row, int col)[] offsets = {
                (-1, -1), (-1, 0), (-1, 1),
                (0, -1),           (0, 1),
                (1, -1),  (1, 0),  (1, 1)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int nr = cell.Row + offsets[i].row;
                int nc = cell.Col + offsets[i].col;

                bool inBounds = nr >= 0 && nr < cellData.Count && nc >= 0 && nc < cellData[0].Count;
                var neighbor = inBounds ? cellData[nr][nc] : new CellData(); // default if not in bounds

                if (offsets[i].row != 0 && offsets[i].col != 0) // Diagonal neighbor
                {
                    if (isAlternate)
                    {
                        if (validColors.Contains(neighbor.ColorName))
                        {
                            diagNeighbors.Add(neighbor);
                        }
                    }
                    else
                    {
                        diagNeighbors.Add(string.IsNullOrEmpty(neighbor.Letter) || neighbor.Letter[0].Equals('=') ? new CellData() : neighbor);
                    }
                }
                else // Cross (non-diagonal) neighbor
                {
                    if (isAlternate)
                    {
                        if (validColors.Contains(neighbor.ColorName))
                        {
                            crossNeighbors.Add(neighbor);
                        }
                    }
                    else
                    {
                        crossNeighbors.Add(string.IsNullOrEmpty(neighbor.Letter) || neighbor.Letter[0].Equals('=') ? new CellData() : neighbor);
                    }

                }
            }

            return (diagNeighbors, crossNeighbors);
        }

        /// <summary>
        /// Calculates the resulting color based on the target color and the current color state.
        /// </summary>
        /// <param name="targetColor">The color intended to be applied.</param>
        /// <param name="currentColor">The existing color state of the cell.</param>
        /// <returns>The new combined color as a string.</returns>
        private static string CalculateColor(string targetColor, string currentColor)
        {
            targetColor = targetColor.ToLower();
            currentColor = currentColor.ToLower();

            if (currentColor == "lightgray")
            {
                return targetColor;
            }
            else if (currentColor == "red" && targetColor != "red")
            {
                return currentColor + "_" + targetColor;
            }
            else if (currentColor == "blue" && targetColor != "blue")
            {
                if (targetColor == "red")
                    return "red_blue";
                else if (targetColor == "green")
                    return "blue_green";
            }
            else if (currentColor == "green" && targetColor != "green")
            {
                return targetColor + "_" + currentColor;
            }
            else if (currentColor == "red_blue")
            {
                if (targetColor == "red" || targetColor == "blue")
                {
                    return "red_blue";
                }
                else if (targetColor == "green")
                {
                    return "red_blue_green";
                }
            }
            else if (currentColor == "red_green")
            {
                if (targetColor == "red" || targetColor == "green")
                {
                    return "red_green";
                }
                else if (targetColor == "blue")
                {
                    return "red_blue_green";
                }
            }
            else if (currentColor == "red_blue_green")
            {
                return "red_blue_green";
            }

            // fallback return original current_color if none of above matched
            return currentColor;
        }

        /// <summary>
        /// Colors a given cell and its neighbors on the canvas according to the specified target color.
        /// </summary>
        /// <param name="cellData">The 2D list representing the puzzle grid cells.</param>
        /// <param name="canvas">The SKCanvas on which to draw.</param>
        /// <param name="colors">Dictionary mapping color names to SKColor values.</param>
        /// <param name="targetColor">The main color to apply ("red", "blue", or "green").</param>
        /// <param name="possibleIndex">The cell whose color and neighbors will be updated.</param>
        /// <param name="cellSize">The size of each cell on the canvas.</param>
        /// <param name="languageDicts">Dictionary containing language-specific data for hint generation.</param>
        /// <returns>Returns true if the coloring was successful; false if the target color is invalid or a drawing error occurs.</returns>
        private static bool ColorHint(List<List<CellData>> cellData, SKCanvas canvas, Dictionary<string, SKColor> colors, string targetColor, CellData possibleIndex, int cellSize, Dictionary<string, object> languageDicts, ref List<PlacedHint> hintsAdded, List<string> inputList, string difficulty)
        {
            if (targetColor != "red" && targetColor != "blue" && targetColor != "green")
                return false; // Invalid color, error

            SKColor darkColor;
            switch (targetColor)
            {
                case "red": darkColor = colors["dark_red"]; break;
                case "blue": darkColor = colors["dark_blue"]; break;
                case "green": darkColor = colors["dark_green"]; break;
                default: return false; // Invalid color, error
            }

            CellData cell = cellData[possibleIndex.Row][possibleIndex.Col];
            cell.Background = darkColor;
            cell.ColorName = targetColor;

            (List<CellData> diagNeighbors, List<CellData> crossNeighbors) = GetNeighbors(cellData, possibleIndex);

            var validNeighborPositions = new List<(int row, int col)>();
            // Filter for valid neighbors
            switch (targetColor)
            {
                case "red":
                    validNeighborPositions = crossNeighbors
                        .Where(n => !string.IsNullOrEmpty(n.Letter) && !n.Letter.StartsWith('='))
                        .Select(n => (row: n.Row, col: n.Col))
                        .ToList();
                    break;
                case "blue":
                    validNeighborPositions = diagNeighbors
                        .Where(n => !string.IsNullOrEmpty(n.Letter) && !n.Letter.StartsWith('='))
                        .Select(n => (row: n.Row, col: n.Col))
                        .ToList();
                    break;
                case "green":
                    validNeighborPositions = diagNeighbors.Concat(crossNeighbors).ToList()
                        .Where(n => !string.IsNullOrEmpty(n.Letter) && !n.Letter.StartsWith('='))
                        .Select(n => (row: n.Row, col: n.Col))
                        .ToList();
                    break;
            }

            var hintValue = CalculateHint(validNeighborPositions, languageDicts, cellData);
            cell.Letter = hintValue;
            cellData[possibleIndex.Row][possibleIndex.Col] = cell;
            int phraseIndex = cellData[possibleIndex.Row][possibleIndex.Col].PhraseIndex;
            var placedHint = new PlacedHint(possibleIndex.Row, possibleIndex.Col, targetColor, validNeighborPositions, hintValue, phraseIndex, difficulty);
            hintsAdded.Add(placedHint);
            if (!ModifyCanvas(canvas, cellData, possibleIndex.Row, possibleIndex.Col, cellSize))
                return false;

            List<CellData> neighborsToColor;
            switch (targetColor)
            {
                case "red": neighborsToColor = crossNeighbors; break;
                case "blue": neighborsToColor = diagNeighbors; break;
                case "green": neighborsToColor = diagNeighbors.Concat(crossNeighbors).ToList(); break;
                default: return false; // Invalid color, error
            }

            for (int i = 0; i < neighborsToColor.Count; i++)
            {
                if (!string.IsNullOrEmpty(neighborsToColor[i].Letter))
                {
                    CellData neighbor = neighborsToColor[i];
                    cell = cellData[neighbor.Row][neighbor.Col];
                    cell.ColorName = CalculateColor(targetColor, cell.ColorName);
                    cell.Background = colors[cell.ColorName];
                    cellData[neighbor.Row][neighbor.Col] = cell;
                    if (!ModifyCanvas(canvas, cellData, neighbor.Row, neighbor.Col, cellSize))
                    {
                        Console.WriteLine("error");
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool CreateHint(List<List<CellData>> cellData, SKCanvas canvas, Dictionary<string, SKColor> colors, int cellSize, Dictionary<string, object> languageDicts, ref List<PlacedHint> hintsAdded, int numHints, ref bool uniquelySolvable, List<string> inputList, string difficulty)
        {
            int nrows = cellData.Count;
            int ncols = cellData[0].Count;
            List<CellData> possibleHints = new();
            string[] baseColors = ["red", "blue", "green"];
            Random rand = new();
            string randomColor = baseColors[rand.Next(3)];
            bool hasValidNeighbor = false;
            bool meetsDifficulty = false;
            var overlapLevels = new Dictionary<string, int> { ["beginner"] = 1, ["novice"] = 2, ["intermediate"] = 3, ["expert"] = 4, ["master"] = 5, ["legendary"] = 6 };

            for (int r = 0; r < nrows; r++)
            {
                for (int c = 0; c < ncols; c++)
                {
                    CellData currentCell = cellData[r][c];
                    bool isEmpty = string.IsNullOrEmpty(currentCell.Letter);
                    var (diagNeighbors, crossNeighbors) = isEmpty ?
                        GetNeighbors(cellData, currentCell) :
                        (new List<CellData> { new(), new(), new(), new() }, new List<CellData> { new(), new(), new(), new() });
                    var allNeighbors = diagNeighbors.Concat(crossNeighbors);
                    var validNeighborPositions = new List<(int row, int col)>(); // Filter for valid neighbors

                    switch (randomColor)
                    {
                        case "red":
                            validNeighborPositions = crossNeighbors
                                .Where(n => !string.IsNullOrEmpty(n.Letter) && !n.Letter.StartsWith('='))
                                .Select(n => (row: n.Row, col: n.Col))
                                .ToList();
                            hasValidNeighbor = validNeighborPositions.Count > 0;
                            break;
                        case "blue":
                            validNeighborPositions = diagNeighbors
                                .Where(cell => !string.IsNullOrEmpty(cell.Letter) && !cell.Letter.StartsWith('='))
                                .Select(cell => (row: cell.Row, col: cell.Col))
                                .ToList();
                            hasValidNeighbor = validNeighborPositions.Count > 0;
                            break;
                        case "green":
                            validNeighborPositions = diagNeighbors.Concat(crossNeighbors)
                                .Where(cell => !string.IsNullOrEmpty(cell.Letter) && !cell.Letter.StartsWith('='))
                                .Select(cell => (row: cell.Row, col: cell.Col))
                                .ToList();
                            hasValidNeighbor = validNeighborPositions.Count > 0;
                            break;
                    }

                    // Check if there is at least one lightgray neighbor
                    // done to the hint provides new information to the player
                    // this is not the only condition for a hint to provide new information
                    // but including other hint positions requires more complex logic
                    bool hasLightGrayNeighbor = validNeighborPositions.Any(pos =>
                        cellData[pos.row][pos.col].ColorName == "lightgray");

                    // Check if the cell meets the difficulty requirement of min neighbors
                    int minCells = 0;
                    if (!overlapLevels.TryGetValue(difficulty, out minCells))
                        throw new ArgumentException($"Invalid difficulty level: {difficulty}");
                    meetsDifficulty = minCells == validNeighborPositions.Count;

                    if (hasValidNeighbor && hasLightGrayNeighbor && meetsDifficulty)
                    {
                        possibleHints.Add(cellData[r][c]);
                    }
                }
            }

            if (possibleHints.Count == 0)
                return false;

            // Find the hint position that is farthest from all existing hints
            CellData bestHint = possibleHints[0];
            double maxMinDistance = 0;

            foreach (var candidate in possibleHints)
            {
                double minDistanceToExistingHints = double.MaxValue;
                
                // Calculate minimum distance to any existing hint
                foreach (var existingHint in hintsAdded)
                {
                    double distance = Math.Sqrt(
                        Math.Pow(candidate.Row - existingHint.Row, 2) + 
                        Math.Pow(candidate.Col - existingHint.Col, 2)
                    );
                    minDistanceToExistingHints = Math.Min(minDistanceToExistingHints, distance);
                }
                
                // If no existing hints, use the first candidate or randomize
                if (hintsAdded.Count == 0)
                {
                    minDistanceToExistingHints = double.MaxValue;
                }
                
                // Select candidate with maximum minimum distance (farthest from any existing hints)
                if (minDistanceToExistingHints > maxMinDistance)
                {
                    maxMinDistance = minDistanceToExistingHints;
                    bestHint = candidate;
                }
                else if (Math.Abs(minDistanceToExistingHints - maxMinDistance) < 0.001) // Equal distances
                {
                    // Break ties randomly
                    if (rand.Next(2) == 0)
                    {
                        bestHint = candidate;
                    }
                }
            }

            Console.WriteLine($"[Hint] Placing hint at ({bestHint.Row},{bestHint.Col}) with min distance {maxMinDistance:F2} from existing hints");

            return ColorHint(cellData, canvas, colors, randomColor, bestHint, cellSize, languageDicts, ref hintsAdded, inputList, difficulty);
        }

        /// <summary>
        /// Clears all letters from the grid (cellData) and optionally updates the canvas,
        /// except for cells where the letter starts with '=' (e.g., hints).
        /// </summary>
        /// <param name="cellData">The 2D list representing the puzzle grid.</param>
        /// <param name="canvas">The SKCanvas on which to redraw the cleared grid. Can be null to skip canvas updates.</param>
        /// <param name="cellSize">The size of each cell in pixels. Ignored if canvas is null.</param>
        private static void ClearGridLetters(ref List<List<CellData>> cellData, SKCanvas? canvas = null, int cellSize = 0)
        {
            int nrows = cellData.Count;
            int ncols = cellData[0].Count;

            for (int r = 0; r < nrows; r++)
            {
                for (int c = 0; c < ncols; c++)
                {
                    var cell = cellData[r][c];

                    // Only clear the letter if it doesn't start with '='
                    if (!string.IsNullOrEmpty(cell.Letter) && !cell.Letter.StartsWith("="))
                    {
                        cell.Letter = "";
                        cellData[r][c] = cell;

                        // Only update canvas if one was provided
                        if (canvas != null)
                        {
                            ModifyCanvas(canvas, cellData, r, c, cellSize);
                        }
                    }
                }
            }
        }

        // This does currently work
        // it outputs all found solutions to their own files
        // Returns true if no alternate solutions found, false if alternate solutions found
        private static (bool isUnique, ((int row, int col) bestIndex, string color) bestHint) makePuzzleUnique(ref List<List<CellData>> cellData, List<string> inputNames, SKCanvas canvas, int cellSize, List<PlacedHint> hintsAdded, Dictionary<int, List<int>> wordsByLength, Dictionary<string, object> languageDicts, List<(int row, int col, bool drawRight, int length, int phraseIndex)> slots, Dictionary<int, (int r, int c)[]> slotPositions)
        {
            Console.WriteLine("Beginning: makePuzzleUnique");
            int nrows = cellData.Count;
            int ncols = cellData[0].Count;
            var charToInt = (Dictionary<char, int>)languageDicts["charToInt"];


            // Store the original cellData state for restoration later
            var originalCellData = cellData.Select(row => new List<CellData>(row)).ToList();

            // Clear the grid letters (but preserve hints) for solving
            ClearGridLetters(ref cellData);

            // Create a working copy of the cleared cellData for the solving algorithm
            var workingGrid = cellData.Select(row => new List<CellData>(row)).ToList();

            Console.WriteLine($"[Solver] Using {slots.Count} pre-built slots with pre-calculated positions");

            // Set up hints and language dictionaries
            var hints = new List<(int row, int col, string value, List<(int, int)> neighbors)>();
            foreach (var placedHint in hintsAdded)
            {
                string hintValue = cellData[placedHint.Row][placedHint.Col].Letter;
                hints.Add((placedHint.Row, placedHint.Col, hintValue, placedHint.NeighborPositions));
            }
            Console.WriteLine($"[Solver] Found {hints.Count} hints.");


            // Stack to manage state for iterative solving
            // Each entry contains: (slotIndex, wordIndex, list of placed positions)
            var stack = new Stack<(int slotIdx, int wordIdx, List<(int r, int c)> placements)>();
            stack.Push((0, -1, new List<(int, int)>())); // Start with first slot, no word tried yet

            Console.WriteLine("[Solver] Starting crossword solver...");

            while (stack.Count > 0)
            {
                var (slotIdx, lastWordIdx, previousPlacements) = stack.Pop();
                //Console.WriteLine("Backtracked");
                // Clear previous placements when backtracking
                foreach (var (r, c) in previousPlacements)
                {
                    var cell = workingGrid[r][c];
                    cell.Letter = "";
                    workingGrid[r][c] = cell;
                }

                if (slotIdx == slots.Count)
                {
                    Console.WriteLine("[Solver] All slots filled. Solution found!");

                    // Check if this solution is different from the original
                    bool isDifferentFromOriginal = false;
                    var differingCells = new List<(int row, int col)>();
                    for (int r = 0; r < nrows; r++)
                    {
                        for (int c = 0; c < ncols; c++)
                        {
                            // Compare the letter content between working grid and original
                            string workingLetter = workingGrid[r][c].Letter;
                            string originalLetter = originalCellData[r][c].Letter;

                            // Skip hint cells (start with =) when comparing
                            if (!string.IsNullOrEmpty(originalLetter) && !originalLetter.StartsWith("="))
                            {
                                if (!string.Equals(workingLetter, originalLetter, StringComparison.OrdinalIgnoreCase))
                                {
                                    isDifferentFromOriginal = true;
                                    differingCells.Add((r, c));
                                    ///Console.WriteLine($"[Solver] Letter difference found at ({r},{c}): working='{workingLetter}' vs original='{originalLetter}'");
                                }
                            }
                        }
                    }

                    // Only add to alternate solutions if it's different from the original
                    if (isDifferentFromOriginal)
                    {
                        Console.WriteLine("[Solver] Found alternate solution!");

                        // Export the alternate solution before returning
                        using var altBitmap = new SKBitmap(ncols * cellSize, nrows * cellSize);
                        using var altCanvas = new SKCanvas(altBitmap);

                        //Restore the original cellData state before returning
                        for (int r = 0; r < nrows; r++)
                        {
                            cellData[r] = new List<CellData>(originalCellData[r]);
                        }
                        UpdateFullCanvas(canvas, cellData, cellSize);

                        return (false, FindCellWithMostNeighborDifferences(cellData, differingCells));
                    }
                    else
                    {
                        Console.WriteLine("[Solver] Found original solution - skipping");
                    }
                    continue;
                }

                var slot = slots[slotIdx];
                //Console.WriteLine($"[Solver] Trying slot #{slotIdx}");

                // Get candidate words for this slot length - optimized lookup
                if (!wordsByLength.TryGetValue(slot.length, out var candidateWordIndices))
                    continue; // No words of this length

                // Try each remaining word of the correct length - optimized to skip already tried words
                foreach (int wordIdx in candidateWordIndices.Where(idx => idx > lastWordIdx))
                {
                    string word = inputNames[wordIdx];

                    // Get pre-calculated coordinate positions for this slot
                    var wordPositions = slotPositions[slotIdx];

                    // Check if word fits
                    bool fits = true;
                    for (int i = 0; i < word.Length; i++)
                    {
                        var (r, c) = wordPositions[i];
                        var cell = workingGrid[r][c];
                        if (!string.IsNullOrEmpty(cell.Letter) && 
                            char.ToLower(cell.Letter[0]) != char.ToLower(word[i]))
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (!fits) continue;

                    // Place the word
                    var placedPositions = new List<(int, int)>();
                    for (int i = 0; i < word.Length; i++)
                    {
                        var (r, c) = wordPositions[i];
                        if (string.IsNullOrEmpty(workingGrid[r][c].Letter))
                        {
                            var cell = workingGrid[r][c];
                            cell.Letter = $"{word[i]}";
                            workingGrid[r][c] = cell;
                            placedPositions.Add((r, c));
                        }
                    }

                    // Check hints
                    bool allHintsOk = true;
                    foreach (var (lr, lc, lval, neighbors) in hints)
                    {
                        // Early validation of hint format to avoid unnecessary processing
                        int expectedMod = -1;
                        if (lval.Length < 2 || !charToInt.TryGetValue(lval[1], out expectedMod))
                        {
                            allHintsOk = false;
                            break;
                        }

                        int sum = 0;
                        int emptyCount = 0;

                        foreach (var (nr, nc) in neighbors)
                        {
                            var letter = workingGrid[nr][nc].Letter;
                            if (!string.IsNullOrEmpty(letter) && charToInt.TryGetValue(letter[0], out int value))
                                sum += value;
                            else if (string.IsNullOrEmpty(letter))
                                emptyCount++;
                        }

                        if (emptyCount == 0)
                        {
                            if (sum % 62 != expectedMod)
                            {
                                allHintsOk = false;
                                break;
                            }
                        }
                        else
                        {
                            int currentMod = sum % 62;
                            int neededDiff = (expectedMod - currentMod + 62) % 62;
                            int maxPossibleIncrease = emptyCount * 61;
                            
                            if (neededDiff > maxPossibleIncrease)
                            {
                                allHintsOk = false;
                                break;
                            }
                        }
                    }

                    if (allHintsOk)
                    {
                        //Console.WriteLine($"[Solver] Placed '{word}' at ({slot.row},{slot.col})");
                        // Save current state for backtracking
                        stack.Push((slotIdx, wordIdx, placedPositions));
                        // Move to next slot
                        stack.Push((slotIdx + 1, -1, new List<(int, int)>()));
                        break;
                    }
                    else
                    {
                        // Undo placement and try next word
                        foreach (var (r, c) in placedPositions)
                        {
                            var cell = workingGrid[r][c];
                            cell.Letter = "";
                            workingGrid[r][c] = cell;
                        }
                    }
                }

                // if (!foundValidWord)
                // {
                    // Console.WriteLine($"[Solver] No valid candidates for slot #{slotIdx}");
                // }
            }

            Console.WriteLine("[Solver] No alternate solutions found.");

            // Restore the original cellData state - optimized row copying
            for (int r = 0; r < nrows; r++)
            {
                cellData[r] = new List<CellData>(originalCellData[r]);
            }
            UpdateFullCanvas(canvas, cellData, cellSize);

            return (true, default); // Return true if no alternate solutions found
        }

        /// <summary>
        /// Finds the cell where neighbors have the most overlapping different letters between two cell grids.
        /// </summary>
        /// <param name="cellData">The original cell grid</param>
        /// <param name="differingCells">List of coordinates where the two grids differ</param>
        /// <returns>A tuple containing the row and column coordinates of the best cell, and a string describing the type</returns>
        private static ((int row, int col), string type) FindCellWithMostNeighborDifferences(List<List<CellData>> cellData, List<(int row, int col)> differingCells)
        {
            List<string> validColors = new List<string> { "lightgray", "red", "blue", "green", "red_blue", "red_green", "blue_green", "red_blue_green" };
            CellData bestCrossCell = new CellData();
            CellData bestDiagCell = new CellData();
            int maxCrossDifferences = 0;
            int maxDiagDifferences = 0;
            Random rand = new Random();

            // Check each differing cell and its neighbors
            for (int diffRow = 0; diffRow < cellData.Count; diffRow++)
            {
                for (int diffCol = 0; diffCol < cellData[0].Count; diffCol++)
                {
                    var currentCell = cellData[diffRow][diffCol];
                    if (!currentCell.ColorName.Equals("white")) continue;
                    var (diagNeighbors, crossNeighbors) = GetNeighbors(cellData, currentCell, true);
                    if (diagNeighbors.Count == 0 && crossNeighbors.Count == 0) continue;

                    int crossDifferenceCount = 0;
                    int diagDifferenceCount = 0;

                    // Count cross neighbors that are in the differing cells list
                    foreach (var neighbor in crossNeighbors)
                    {
                        if (differingCells.Contains((neighbor.Row, neighbor.Col)))
                        {
                            crossDifferenceCount++;
                        }
                    }

                    // Count diagonal neighbors that are in the differing cells list
                    foreach (var neighbor in diagNeighbors)
                    {
                        if (differingCells.Contains((neighbor.Row, neighbor.Col)))
                        {
                            diagDifferenceCount++;
                        }
                    }

                    // Update best cross cell if this one has more cross differences
                    if (crossDifferenceCount > maxCrossDifferences)
                    {
                        maxCrossDifferences = crossDifferenceCount;
                        bestCrossCell = currentCell;
                    }

                    // Update best diagonal cell if this one has more diagonal differences
                    if (diagDifferenceCount > maxDiagDifferences)
                    {
                        maxDiagDifferences = diagDifferenceCount;
                        bestDiagCell = currentCell;
                    }
                }
            }

            // Choose the best overall cell (cross vs diagonal)
            (int row, int col) bestPosition;
            string bestType;

            if (maxCrossDifferences > maxDiagDifferences)
            {
                bestPosition = (bestCrossCell.Row, bestCrossCell.Col);
                bestType = "red";
                Console.WriteLine($"[Analysis] Found best cross cell at ({bestPosition.row},{bestPosition.col}) with {maxCrossDifferences} differing cross neighbors");
            }
            else if (maxCrossDifferences < maxDiagDifferences)
            {
                bestPosition = (bestDiagCell.Row, bestDiagCell.Col);
                bestType = "blue";
                Console.WriteLine($"[Analysis] Found best diagonal cell at ({bestPosition.row},{bestPosition.col}) with {maxDiagDifferences} differing diagonal neighbors");
            }
            else
            {
                // Equal counts - choose randomly
                var bestCell = rand.Next(2) == 0 ? bestCrossCell : bestDiagCell;
                bestPosition = (bestCell.Row, bestCell.Col);
                bestType = bestCell.Equals(bestCrossCell) ? "red" : "blue";
                Console.WriteLine($"[Analysis] Randomly selected {bestType} cell at ({bestPosition.row},{bestPosition.col}) with {maxCrossDifferences} differing neighbors");
            }

            // Only randomly override if the other cell type also has some differing neighbors
            var (greenDiagNeighbors, greenCrossNeighbors) = GetNeighbors(cellData, new CellData(row: bestPosition.row, col: bestPosition.col), true);
            bool hasLightGrey = false;
            if (bestType.Equals("red") && greenDiagNeighbors.Count > 0)
            {
                hasLightGrey = greenDiagNeighbors.Any(n => n.ColorName.Equals("lightgray", StringComparison.OrdinalIgnoreCase));
            }
            else if (bestType.Equals("blue") && greenCrossNeighbors.Count > 0)
            {
                hasLightGrey = greenCrossNeighbors.Any(n => n.ColorName.Equals("lightgray", StringComparison.OrdinalIgnoreCase));
            }
            if (hasLightGrey && rand.Next(100) < 15)
            {
                bestType = "green";
                Console.WriteLine($"[Analysis] Switched to diagonal cell due to lightgray neighbor at ({bestPosition.row},{bestPosition.col})");
            }
            // Old code that does not take into account the green adding new info (has new lightgray cell)
            // if (maxCrossDifferences > 0 && maxDiagDifferences > 0 && rand.Next(100) < 20)
            // {
            //     bestType = "green";
            //     Console.WriteLine($"[Analysis] Randomly overriding to green for cell at ({bestPosition.row},{bestPosition.col})");
            // }

            return (bestPosition, bestType);
        }
        
    }
}