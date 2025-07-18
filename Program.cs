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

        public CellData(string letter = "", SKColor? background = null, string colorName = "white", int row = 0, int col = 0, int phraseIndex = 0, bool drawRight = true, int baseRow = 0, int baseCol = 0)
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
    /// Represents a limiter placed on the crossword grid, including its position, color, neighbor positions, and limiter value.
    /// </summary>
    struct PlacedLimiter
    {
        public int Row;
        public int Col;
        public string Color;
        public List<(int row, int col)> NeighborPositions;
        public string LimiterValue;
        public int PhraseIndex;
        public string Difficulty;
        public PlacedLimiter(int row, int col, string color, List<(int, int)> neighborPositions, string limiterValue, int phraseIndex, string difficulty)
        {
            Row = row;
            Col = col;
            Color = color;
            NeighborPositions = neighborPositions;
            LimiterValue = limiterValue;
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
            int numWords = 17; // Number of words to place in the puzzle
            int numLimiters = 7;
            string filename = "crossword";
            string extension = ".png";

            // key is difficulty level, value is weight
            // difficulty for each limiter is randomly selected based on these weights
            var difficultyWeights = new Dictionary<string, float>
            {
                { "beginner", 0.3f },
                { "novice", 0.5f },
                { "intermediate", 0.2f },
                { "expert", 0.0f },
                { "master", 0.0f },
                { "legendary", 0.0f }
            };

            using var bitmap = new SKBitmap(ncols * cellSize, nrows * cellSize);
            using var canvas = new SKCanvas(bitmap);

            InitializeGrid(ref cellData, nrows, ncols); //proper error handling is needed
            InitilizeMappings(intToCharMapping, charToIntMapping);
            define_colors(colors);
            InitilizeCanvas(nrows, ncols, canvas, cellSize);

            CreatePuzzle(ref cellData, inputNames, canvas, languageDicts, colors, cellSize, numWords: numWords, numLimiters: numLimiters, difficultyWeights: difficultyWeights);
            ExportPuzzle(bitmap, filename: filename + extension);

            ClearGridLetters(ref cellData, canvas, cellSize);
            ExportPuzzle(bitmap, filename: filename + "_blank" + extension);


            /*for (int i = 0; i < 1; i++) //Example code for creating multiple puzzles
            {
                reset_variables(ref cellData, ref inputs_added);
                initialize_grid(ref cellData, nrows, ncols); //proper error handling is needed
                initilize_canvas(nrows, ncols, canvas, cellSize);
                create_puzzle(ref cellData, input_names, canvas, language_dicts, colors, cellSize, num_words: num_words, num_limiters: num_limiters, difficulty: difficulty);
                //export_puzzle(bitmap, filename: "tmp" + i.ToString() + ".png");
            }*/
        }

        /// <summary>
        /// Generates a crossword puzzle by placing words and limiters on the grid and drawing them on the canvas.
        /// </summary>
        /// <param name="cellData">The crossword grid to populate.</param>
        /// <param name="inputNames">List of possible input words.</param>
        /// <param name="canvas">The canvas to draw the puzzle on.</param>
        /// <param name="languageDicts">Dictionaries used for encoding letters (e.g., char-to-int mapping).</param>
        /// <param name="colors">Mapping of color names to SKColor values.</param>
        /// <param name="cellSize">Size in pixels of each grid cell.</param>
        /// <param name="numWords">Number of words to place in the puzzle.</param>
        /// <param name="numLimiters">Number of limiter elements to generate.</param>
        /// <param name="difficulty">String indicating puzzle difficulty level.</param>
        /// <returns>The updated crossword grid with placed words and limiters.</returns>
        private static List<List<CellData>> CreatePuzzle(
            ref List<List<CellData>> cellData,
            List<string> inputNames,
            SKCanvas canvas,
            Dictionary<string, object> languageDicts,
            Dictionary<string, SKColor> colors,
            int cellSize,
            int numWords,
            int numLimiters,
            Dictionary<string, float> difficultyWeights)
        {
            if (numWords <= 0)
                throw new ArgumentException("numWords must be greater than 0");
            if (numLimiters <= 0)
                throw new ArgumentException("numLimiters must be greater than 0");
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
            List<PlacedLimiter> limitersAdded = new List<PlacedLimiter>();

            inputsAdded = DrawSeedWord(ref cellData, inputNames, canvas, cellSize);

            for (int i = 0; i < numWords - 1; i++)
            {
                DrawRandomWord(ref cellData, inputNames, ref inputsAdded, canvas, cellSize);
            }

            bool uniquelySolvable = false;
            for (int i = 0; i < numLimiters; i++)
            {
                // valid difficulties: "beginner", "novice", "intermediate", "expert", "master", "legendary"
                string difficulty = weightedPool.OrderBy(_ => rand.Next()).ToList()[0];
                Console.WriteLine($"Selected difficulty: {difficulty}");
                CreateLimiter(cellData, canvas, colors, cellSize, languageDicts, ref limitersAdded, numLimiters, ref uniquelySolvable, inputNames, difficulty);
            }

            if (!uniquelySolvable)
            {
                Console.WriteLine("WARNING: Puzzle may not have a unique solution.");
            }
            else
            {
                Console.WriteLine("Puzzle has a unique solution.");
            }

            return cellData;
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
        /// Computes a limiter string based on the sum of encoded character values at specified grid positions.
        /// </summary>
        /// <param name="pickedBorderIndex">List of (row, column) positions to include in the calculation.</param>
        /// <param name="languageDicts">Dictionary containing character-to-integer and integer-to-character mappings.</param>
        /// <param name="cellData">The crossword grid containing letters used for calculation.</param>
        /// <returns>
        /// A string starting with '=' followed by a character representing the encoded sum modulo 62.
        /// </returns>
        private static string CalculateLimiter(List<(int, int)> pickedBorderIndex, Dictionary<string, object> languageDicts, List<List<CellData>> cellData)
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
        private static void define_colors(Dictionary<string, SKColor> colors)
        {
            colors.Add("red", new SKColor(243, 174, 172));
            colors.Add("blue", new SKColor(170, 170, 249));
            colors.Add("green", new SKColor(189, 253, 178));
            colors.Add("dark_red", new SKColor(218, 100, 95));
            colors.Add("dark_blue", new SKColor(90, 90, 227));
            colors.Add("dark_green", new SKColor(132, 232, 110));
            colors.Add("red_blue", new SKColor(212, 168, 212));
            colors.Add("red_green", new SKColor(212, 212, 168));
            colors.Add("blue_green", new SKColor(168, 212, 212));
            colors.Add("red_blue_green", new SKColor(197, 197, 197));
        }

        static void InitilizeCanvas(int nrows, int ncols, SKCanvas canvas, int cellSize)
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
        /// Initilizes two maps containing conversion info to and from codeduku ascii.
        /// </summary>
        /// <param name="intToCharMapping">The map that converts ints to their equivalent char representation.</param>
        /// <param name="charToIntMapping">The map that converts chars to their equivalent int representation.</param>
        /// <returns>void.</returns>
        private static void InitilizeMappings(Dictionary<int, char> intToCharMapping, Dictionary<char, int> charToIntMapping)
        {
            int total = 0;
            for (int i = 48; i <= 57; i++) // 0 - 9
            {
                intToCharMapping.Add(total + (i - 48), (char)i);
                charToIntMapping.Add((char)i, total + (i - 48));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 48)));
            }
            total += (57 - 48) + 1;
            for (int i = 97; i <= 122; i++) // a - z
            {
                intToCharMapping.Add(total + (i - 97), (char)i);
                charToIntMapping.Add((char)i, total + (i - 97));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 97)));
            }
            total += (90 - 65) + 1;
            for (int i = 65; i <= 90; i++) // A - Z
            {
                intToCharMapping.Add(total + (i - 65), (char)i);
                charToIntMapping.Add((char)i, total + (i - 65));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i - 65)));
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
        private static (List<CellData> diag_neighbors, List<CellData> cross_neighbors) GetNeighbors(List<List<CellData>> cellData, CellData cell)
        {
            // todo: modify to do somthing different if the cell passed in is default
            var diagNeighbors = new List<CellData>();
            var crossNeighbors = new List<CellData>();

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
                    diagNeighbors.Add(string.IsNullOrEmpty(neighbor.Letter) || neighbor.Letter[0].Equals('=') ? new CellData() : neighbor);
                }
                else // Cross (non-diagonal) neighbor
                {
                    crossNeighbors.Add(string.IsNullOrEmpty(neighbor.Letter) || neighbor.Letter[0].Equals('=') ? new CellData() : neighbor);
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
            else if (currentColor != "red" && currentColor != "blue" && currentColor != "green")
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
        /// <param name="languageDicts">Dictionary containing language-specific data for limiter generation.</param>
        /// <returns>Returns true if the coloring was successful; false if the target color is invalid or a drawing error occurs.</returns>
        private static bool ColorLimiter(List<List<CellData>> cellData, SKCanvas canvas, Dictionary<string, SKColor> colors, string targetColor, CellData possibleIndex, int cellSize, Dictionary<string, object> languageDicts, ref List<PlacedLimiter> limitersAdded, List<string> inputList, string difficulty)
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

            var limiterValue = CalculateLimiter(validNeighborPositions, languageDicts, cellData);
            cell.Letter = limiterValue;
            cellData[possibleIndex.Row][possibleIndex.Col] = cell;
            int phraseIndex = cellData[possibleIndex.Row][possibleIndex.Col].PhraseIndex;
            var placedLimiter = new PlacedLimiter(possibleIndex.Row, possibleIndex.Col, targetColor, validNeighborPositions, limiterValue, phraseIndex, difficulty);
            limitersAdded.Add(placedLimiter);
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

        private static bool CreateLimiter(List<List<CellData>> cellData, SKCanvas canvas, Dictionary<string, SKColor> colors, int cellSize, Dictionary<string, object> languageDicts, ref List<PlacedLimiter> limitersAdded, int numLimiters, ref bool uniquelySolvable, List<string> inputList, string difficulty)
        {
            int nrows = cellData.Count;
            int ncols = cellData[0].Count;
            List<CellData> possibleLimiters = new();
            string[] baseColors = ["red", "blue", "green"];
            Random rand = new();
            string randomColor = baseColors[rand.Next(3)];
            bool hasValidNeighbor = false;
            bool meetsDifficulty = false;
            var overlapLevels = new Dictionary<string, int> { ["beginner"] = 1, ["novice"] = 2, ["intermediate"] = 3, ["expert"] = 4, ["master"] = 5 , ["legendary"] = 6 };

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
                    bool uniqueSolution = false;
                    var validNeighborPositions = new List<(int row, int col)>(); // Filter for valid neighbors

                    switch (randomColor)
                    {
                        case "red":
                            hasValidNeighbor = crossNeighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.Letter) &&
                                !cell.Letter.StartsWith("="));
                            validNeighborPositions = crossNeighbors
                                .Where(n => !string.IsNullOrEmpty(n.Letter) && !n.Letter.StartsWith('='))
                                .Select(n => (row: n.Row, col: n.Col))
                                .ToList();
                            break;
                        case "blue":
                            hasValidNeighbor = diagNeighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.Letter) &&
                                !cell.Letter.StartsWith("="));
                            validNeighborPositions = diagNeighbors
                                .Where(cell => !string.IsNullOrEmpty(cell.Letter) && !cell.Letter.StartsWith('='))
                                .Select(cell => (row: cell.Row, col: cell.Col))
                                .ToList();
                            break;
                        case "green":
                            hasValidNeighbor = allNeighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.Letter) &&
                                !cell.Letter.StartsWith("="));
                            validNeighborPositions = diagNeighbors.Concat(crossNeighbors).ToList()
                                .Where(cell => !string.IsNullOrEmpty(cell.Letter) && !cell.Letter.StartsWith('='))
                                .Select(cell => (row: cell.Row, col: cell.Col))
                                .ToList();
                            break;
                    }

                    // Check if there is at least one lightgray neighbor
                    // done to the limiter provides new information to the player
                    // this is not the only condition for a limiter to provide new information
                    // but including other limiter positions requires more complex logic
                    bool hasLightGrayNeighbor = validNeighborPositions.Any(pos =>
                        cellData[pos.row][pos.col].ColorName == "lightgray");

                    uniqueSolution = VerifyUnique(cellData, validNeighborPositions, limitersAdded, numLimiters, ref uniquelySolvable, languageDicts, inputList, hasValidNeighbor, hasLightGrayNeighbor);

                    // Check if the cell meets the difficulty requirement of min neighbors
                    int minCells = 0;
                    if (!overlapLevels.TryGetValue(difficulty, out minCells))
                        throw new ArgumentException($"Invalid difficulty level: {difficulty}");
                    meetsDifficulty = minCells == validNeighborPositions.Count;

                    if (hasValidNeighbor && hasLightGrayNeighbor && uniqueSolution && meetsDifficulty)
                    {
                        possibleLimiters.Add(cellData[r][c]);
                    }
                }
            }

            if (possibleLimiters.Count == 0)
                return false;

            possibleLimiters = possibleLimiters.OrderBy(_ => rand.Next()).ToList();

            return ColorLimiter(cellData, canvas, colors, randomColor, possibleLimiters[0], cellSize, languageDicts, ref limitersAdded, inputList, difficulty);
        }

        /// <summary>
        /// Checks whether the current limiter placement makes the solution uniquely solvable.
        /// Updates the 'uniquelySolvable' flag accordingly.
        /// Returns false if validation cannot proceed (e.g., not all neighbors are from the same phrase).
        /// return value is always true if (limitersAdded.Count != numLimiters - 1). Intended.
        /// </summary>
        private static bool VerifyUnique(
            List<List<CellData>> cellData,
            List<(int row, int col)> validNeighborPositions,
            List<PlacedLimiter> limitersAdded,
            int numLimiters,
            ref bool uniquelySolvable,
            Dictionary<string, object> languageDicts,
            List<string> inputList,
            bool hasValidNeighbor,
            bool hasLightGrayNeighbor)
        {
            if ((limitersAdded.Count == numLimiters - 1) && !uniquelySolvable) // final limiter
            {
                if (validNeighborPositions.Count > 0 && hasValidNeighbor && hasLightGrayNeighbor)
                {
                    List<string> matchesIntersections = new();
                    int baseRow = cellData[validNeighborPositions[0].row][validNeighborPositions[0].col].BaseRow;
                    int baseCol = cellData[validNeighborPositions[0].row][validNeighborPositions[0].col].BaseCol;
                    int firstPhraseIndex = cellData[baseRow][baseCol].PhraseIndex;
                    bool allSamePhrase = validNeighborPositions.All(pos => cellData[pos.row][pos.col].PhraseIndex == firstPhraseIndex);
                    var limiterValueOriginal = CalculateLimiter(validNeighborPositions, languageDicts, cellData);
                    if (!allSamePhrase)
                    {
                        return false;
                    }
                    var candidates = inputList.Where(w => w.Length == inputList[firstPhraseIndex].Length && w != inputList[firstPhraseIndex]).ToList();
                    var cellDataCopy = cellData.Select(row => row.Select(cell => cell).ToList()).ToList();

                    // Determine indexes in the word where there is an adjacent (non-empty) cell
                    List<int> constrainedPositions = new();
                    for (int i = 0; i < inputList[firstPhraseIndex].Length; i++)
                    {
                        int tmpRow = baseRow + (cellData[baseRow][baseCol].DrawRight ? 0 : i);
                        int tmpCol = baseCol + (cellData[baseRow][baseCol].DrawRight ? i : 0);

                        if (cellData[baseRow][baseCol].DrawRight) // Check above and below
                        {
                            bool aboveHasValue = tmpRow > 0 && !string.IsNullOrEmpty(cellData[tmpRow - 1][tmpCol].Letter);
                            bool belowHasValue = tmpRow < cellData.Count - 1 && !string.IsNullOrEmpty(cellData[tmpRow + 1][tmpCol].Letter);
                            if (aboveHasValue || belowHasValue)
                                constrainedPositions.Add(i);
                        }
                        else // Check left and right
                        {
                            bool leftHasValue = tmpCol > 0 && !string.IsNullOrEmpty(cellData[tmpRow][tmpCol - 1].Letter);
                            bool rightHasValue = tmpCol < cellData[0].Count - 1 && !string.IsNullOrEmpty(cellData[tmpRow][tmpCol + 1].Letter);
                            if (leftHasValue || rightHasValue)
                                constrainedPositions.Add(i);
                        }
                    }
                    // For each candidate, check if the constrainedPositions match between the base word and the candidate
                    foreach (var candidate in candidates)
                    {
                        bool allMatch = constrainedPositions.All(pos => candidate[pos] == inputList[firstPhraseIndex][pos]);
                        if (allMatch)
                        {
                            matchesIntersections.Add(candidate);
                        }
                    }

                    // verify limiter value calculation at indexes results in different answer
                    foreach (var match in matchesIntersections)
                    {
                        for (int i = 0; i < match.Length; i++)
                        {
                            int tmpRow = baseRow + (cellData[baseRow][baseCol].DrawRight ? 0 : i);
                            int tmpCol = baseCol + (cellData[baseRow][baseCol].DrawRight ? i : 0);

                            var cellCopy = cellDataCopy[tmpRow][tmpCol];
                            cellCopy.Letter = match[i].ToString();
                            cellDataCopy[tmpRow][tmpCol] = cellCopy;
                        }
                        var limiterValue = CalculateLimiter(validNeighborPositions, languageDicts, cellDataCopy);
                        if (limiterValue == limiterValueOriginal)
                        {
                            return false;
                        }
                        uniquelySolvable = true;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Checks if a given phrase placement is unique in the crossword context.
        /// </summary>
        /// <param name="cellData">The crossword grid.</param>
        /// <param name="phraseInfo">The phrase placement to check.</param>
        /// <param name="inputNames">All possible input words.</param>
        /// <returns>True if the phrase placement is unique, false otherwise.</returns>
        private static bool IsLimiterUnique(List<List<CellData>> cellData, PhraseStruct phraseInfo, List<string> inputNames)
        {
            string baseWord = phraseInfo.Phrase;
            foreach (var candidate in inputNames)
            {
                if (candidate.Length != baseWord.Length || candidate == baseWord)
                    continue;

                // Add further uniqueness logic here as needed.
            }
            // Placeholder: always returns true for now.
            return true;
        }



        /// <summary>
        /// Resets key variables to their initial state for puzzle generation.
        /// </summary>
        /// <param name="cellData">The crossword grid to be cleared and reset.</param>
        /// <param name="inputsAdded">The list of inputs that have already been added to the grid.</param>
        private static void ResetVariables(ref List<List<CellData>> cellData, ref List<PhraseStruct> inputsAdded)
        {
            cellData = new List<List<CellData>>();
            inputsAdded = new List<PhraseStruct>();
        }

        /// <summary>
        /// Clears all letters from the grid (cellData) and updates the canvas,
        /// except for cells where the letter starts with '=' (e.g., limiters).
        /// </summary>
        /// <param name="cellData">The 2D list representing the puzzle grid.</param>
        /// <param name="canvas">The SKCanvas on which to redraw the cleared grid.</param>
        /// <param name="cellSize">The size of each cell in pixels.</param>
        private static void ClearGridLetters(ref List<List<CellData>> cellData, SKCanvas canvas, int cellSize)
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

                        ModifyCanvas(canvas, cellData, r, c, cellSize);
                    }
                }
            }
        }
    }
}