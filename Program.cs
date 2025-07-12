using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace codeduke
{
    /// <summary>
    /// Represents a single cell in the crossword grid, including its string, color, and position.
    /// </summary>
    struct CellData
    {
        public string letter;         // The string stored in the cell.
        public SKColor background;    // The background color of the cell (used for canvas).
        public string color_name;     // The name of the color used (for tracking purposes).
        public int row;               // row index of letter.
        public int col;               // row index of letter.

        public CellData(string letter = "", SKColor? background = null, string color_name = "white", int row = 0, int col = 0)
        {
            this.letter = letter;
            this.background = background ?? SKColors.White;
            this.color_name = color_name;
            this.row = row;
            this.col = col;
        }
    }
    
    /// <summary>
    /// Represents a word to be drawn on the grid, including its content, position, and orientation.
    /// Created to simplify values being passed and returned by functions
    /// </summary>
    struct phrase_struct
    {
        public string phrase;    // The string stored in the cell.
        public int row;          // row index of phrase.
        public int col;          // col index of phrase.
        public bool draw_right;  // Direction: true for horizontal, false for vertical.

        public phrase_struct(string phrase = "", int row = 0, int col = 0, bool draw_right = true)
        {
            this.phrase = phrase;
            this.row = row;
            this.col = col;
            this.draw_right = draw_right;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<char, int> char_to_int_mapping = new();
            Dictionary<int, char> int_to_char_mapping = new();
            Dictionary<string, object> language_dicts = new();
            List<string> inputs_added = new();
            List<string> input_names = get_names(@"../../../../../input_names.txt");
            Dictionary<string, SKColor> colors = new();

            language_dicts["char_to_int"] = char_to_int_mapping;
            language_dicts["int_to_char"] = int_to_char_mapping;

            int nrows = 20, ncols = 20, cellSize = 50;
            int num_words = 10;
            int num_limiters = 8; // need to account for red, green and blue overlaping
            string difficulty = "normal";
            string filename = "crossword.png";
            
            List<List<CellData>> cellData = new();
            
            using var bitmap = new SKBitmap(ncols * cellSize, nrows * cellSize);
            using var canvas = new SKCanvas(bitmap);

            initialize_grid(ref cellData, nrows, ncols); //proper error handling is needed
            initilize_mappings(int_to_char_mapping, char_to_int_mapping);
            define_colors(colors);
            initilize_canvas(nrows, ncols, canvas, cellSize);

            cellData = create_puzzle(ref cellData, input_names, canvas, language_dicts, colors, cellSize, num_words: num_words, num_limiters: num_limiters, difficulty: difficulty);
            export_puzzle(bitmap, filename: filename);

            /*for (int i = 0; i < 1; i++)
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
        /// <param name="input_names">List of possible input words.</param>
        /// <param name="canvas">The canvas to draw the puzzle on.</param>
        /// <param name="language_dicts">Dictionaries used for encoding letters (e.g., char-to-int mapping).</param>
        /// <param name="colors">Mapping of color names to SKColor values.</param>
        /// <param name="cellSize">Size in pixels of each grid cell.</param>
        /// <param name="num_words">Number of words to place in the puzzle.</param>
        /// <param name="num_limiters">Number of limiter elements to generate.</param>
        /// <param name="difficulty">String indicating puzzle difficulty level.</param>
        /// <returns>The updated crossword grid with placed words and limiters.</returns>
        static List<List<CellData>> create_puzzle(
            ref List<List<CellData>> cellData,
            List<string> input_names,
            SKCanvas canvas,
            Dictionary<string, object> language_dicts,
            Dictionary<string, SKColor> colors,
            int cellSize,
            int num_words,
            int num_limiters,
            string difficulty)
        {
            if (num_words <= 0)
                throw new ArgumentException("num_words must be greater than 0");

            if (num_limiters <= 0)
                throw new ArgumentException("num_limiters must be greater than 0");

            List<string> inputs_added = new List<string>();

            // Place the first random word
            var placeResult = place_random_word(cellData, input_names);
            if (placeResult.Item1)
            {
                var phraseInfo = placeResult.Item2;
                inputs_added = new List<string> { phraseInfo.phrase };
                draw_string(ref cellData, phraseInfo, canvas, cellSize, colors);
            }

            // Place the remaining words
            for (int i = 0; i < num_words - 1; i++)
            {
                var drawResult = get_random_draw(cellData, input_names, ref inputs_added);
                if (drawResult.success)
                {
                    var phraseInfo = drawResult.phraseInfo;
                    draw_string(ref cellData, phraseInfo, canvas, cellSize, colors);
                }
            }

            // Create limiters
            for (int i = 0; i < num_limiters; i++)
            {
                create_limiter_2(cellData, canvas, colors, cellSize, language_dicts);
            }

            return cellData;
        }
        
        /// <summary>
        /// Attempts to find a valid position and draw a randomly selected word from the input list onto the crossword grid.
        /// used for setting the "seed" of the crossword
        /// </summary>
        /// <param name="cellData">2D list representing the crossword grid.</param>
        /// <param name="input_names">List of all input words to possibly draw.</param>
        /// <param name="inputs_added">Reference list of words that have already been placed.</param>
        /// <returns>
        /// Tuple:
        /// - success (bool): True if a word was successfully placed.
        /// - phraseInfo (phrase_struct): The placement information for the selected word.
        /// </returns>
        public static (bool success, phrase_struct phraseInfo) get_random_draw(
            List<List<CellData>> cellData,
            List<string> input_names,
            ref List<string> inputs_added)
        {
            Random rand = new Random();
            var inputs_added_copy = inputs_added;
            List<bool> draw_right_values = new List<bool>(){true, false};
            draw_right_values = draw_right_values.OrderBy(_ => rand.Next()).ToList();
            

            // Filter out words that have already been added and shuffle the remaining candidates
            var candidates = input_names
                .Where(name => !inputs_added_copy.Contains(name))
                .OrderBy(_ => rand.Next())
                .ToList();

            int nrows = cellData.Count;
            int ncols = cellData[0].Count;

            // Try to place each candidate word
            foreach (var random_input in candidates)
            {
                draw_right_values = draw_right_values.OrderBy(_ => rand.Next()).ToList();
                foreach (bool draw_right in draw_right_values)
                {
                    // Determine valid placement bounds based on word orientation
                    int max_row = draw_right ? nrows : nrows - random_input.Length;
                    int max_col = draw_right ? ncols - random_input.Length : ncols;

                    // Try every grid position within bounds
                    for (int row = 0; row < max_row; row++)
                    {
                        for (int col = 0; col < max_col; col++)
                        {
                            var phrase_info = new phrase_struct(random_input, row, col, draw_right);

                            // Check basic placement rules
                            if (!draw_string_check(cellData, phrase_info))
                                continue;

                            // Ensure there is at least one overlapping letter
                            bool has_overlap = false;
                            for (int j = 0; j < random_input.Length; j++)
                            {
                                int r = row + (draw_right ? 0 : j);
                                int c = col + (draw_right ? j : 0);
                                if (!string.IsNullOrEmpty(cellData[r][c].letter) &&
                                    cellData[r][c].letter.ToLower() == random_input[j].ToString().ToLower())
                                {
                                    has_overlap = true;
                                    break;
                                }
                            }

                            if (!has_overlap) continue;

                            // Check for visual spacing conflicts (letters placed directly parallel)
                            bool has_parallel_conflict = false;
                            for (int j = 0; j < random_input.Length; j++)
                            {
                                int r = row + (draw_right ? 0 : j);
                                int c = col + (draw_right ? j : 0);

                                if (!string.IsNullOrEmpty(cellData[r][c].letter) &&
                                    cellData[r][c].letter == random_input[j].ToString())
                                {
                                    continue;
                                }

                                if (draw_right)
                                {
                                    if ((row > 0 && !string.IsNullOrEmpty(cellData[row - 1][c].letter)) ||
                                        (row < nrows - 1 && !string.IsNullOrEmpty(cellData[row + 1][c].letter)))
                                    {
                                        has_parallel_conflict = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    if ((col > 0 && !string.IsNullOrEmpty(cellData[r][col - 1].letter)) ||
                                        (col < ncols - 1 && !string.IsNullOrEmpty(cellData[r][col + 1].letter)))
                                    {
                                        has_parallel_conflict = true;
                                        break;
                                    }
                                }
                            }

                            if (has_parallel_conflict) continue;

                            // All checks passed; record the placement and return
                            inputs_added.Add(random_input);
                            return (true, phrase_info);
                        }
                    }

                }

            
            }

            // No valid position found
            return (false, new phrase_struct());
        }
        
        /// <summary>
        /// Attempts to place a randomly selected word on the grid in a valid location and orientation.
        /// Must overlap an existing word (key constraint)
        /// </summary>
        /// <param name="cellData">The crossword grid.</param>
        /// <param name="input_names">List of available words to choose from.</param>
        /// <returns>
        /// A tuple containing:
        /// - success (bool): True if a word was successfully placed.
        /// - phraseInfo (phrase_struct): The placement information.
        /// - inputsAdded (List<string>): List containing the added word if placement was successful.
        /// </returns>
        public static (bool success, phrase_struct phraseInfo, List<string> inputsAdded) place_random_word(
            List<List<CellData>> cellData,
            List<string> input_names)
        {
            Random rand = new();
            List<bool> options = new() { true, false };

            bool draw_right = options[rand.Next(2)];

            int nrows = cellData.Count;
            int ncols = cellData[0].Count;

            List<string> filtered_input_names = draw_right
                ? input_names.Where(name => name.Length <= ncols).ToList()
                : input_names.Where(name => name.Length <= nrows).ToList();

            if (filtered_input_names.Count == 0)
                return (false, new phrase_struct(), new List<string>());

            string random_input = filtered_input_names[rand.Next(filtered_input_names.Count)];

            int max_row = draw_right ? nrows : nrows - random_input.Length;
            int max_col = draw_right ? ncols - random_input.Length : ncols;

            int rand_row = rand.Next(0, max_row);
            int rand_col = rand.Next(0, max_col);

            var phraseInfo = new phrase_struct(random_input, rand_row, rand_col, draw_right);

            if (draw_string_check(cellData, phraseInfo))
            {
                return (true, phraseInfo, new List<string> { random_input });
            }

            return (false, new phrase_struct(), new List<string>());
        }
        
        /// <summary>
        /// Computes a limiter string based on the sum of encoded character values at specified grid positions.
        /// </summary>
        /// <param name="picked_border_index">List of (row, column) positions to include in the calculation.</param>
        /// <param name="language_dicts">Dictionary containing character-to-integer and integer-to-character mappings.</param>
        /// <param name="cellData">The crossword grid containing letters used for calculation.</param>
        /// <returns>
        /// A string starting with '=' followed by a character representing the encoded sum modulo 62.
        /// </returns>
        static string calculate_limiter(List<(int, int)> picked_border_index, Dictionary<string, object> language_dicts, List<List<CellData>> cellData)
        {
            var char_to_int = (Dictionary<char, int>)language_dicts["char_to_int"];
            var int_to_char = (Dictionary<int, char>)language_dicts["int_to_char"];

            int sum = 0;

            foreach (var (r, c) in picked_border_index)
            {
                string letter = cellData[r][c].letter;

                if (!string.IsNullOrEmpty(letter))
                {
                    char ch = letter[0]; // assumes letter is a single-character string
                    if (char_to_int.ContainsKey(ch))
                    {
                        sum += char_to_int[ch];
                    }
                }
            }

            int modValue = sum % 62;
            char summed_value = int_to_char[modValue];

            return "=" + summed_value;
        }

        /// <summary>
        /// Populates the provided dictionary with named SKColor values used in the crossword puzzle.
        /// </summary>
        /// <param name="colors">A dictionary to store color names and their corresponding SKColor values.</param>
        static void define_colors(Dictionary<string, SKColor> colors)
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

        static void initilize_canvas(int nrows, int ncols, SKCanvas canvas, int cellSize)
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
        /// <param name="file_name">Path to the input text file.</param>
        /// <returns>List of cleaned words.</returns>
        public static List<string> get_names(string file_name)
        {
            List<string> output_list = new List<string>();
            output_list = File.ReadAllLines(file_name).ToList();
            output_list = output_list.Select(s => s.Replace('ä', 'a')).ToList(); // one string in the file has this
            return output_list;
        }

        /// <summary>
        /// Initilizes two maps containing conversion info to and from codeduku ascii.
        /// </summary>
        /// <param name="int_to_char_mapping">The map that converts ints to their equivalent char representation.</param>
        /// <param name="char_to_int_mapping">The map that converts chars to their equivalent int representation.</param>
        /// <returns>void.</returns>
        public static void initilize_mappings(Dictionary<int, char> int_to_char_mapping, Dictionary<char, int> char_to_int_mapping)
        {
            int total = 0;
            for (int i = 48; i <= 57; i++) // 0 - 9
            {
                int_to_char_mapping.Add(total + (i - 48), (char)i);
                char_to_int_mapping.Add((char)i, total + (i - 48));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i-48)));
            }
            total += (57 - 48) + 1;
            for (int i = 97; i <= 122; i++) // a - z
            {
                int_to_char_mapping.Add(total + (i - 97), (char)i);
                char_to_int_mapping.Add((char)i, total + (i - 97));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i-97)));
            }
            total += (90 - 65) + 1;
            for (int i = 65; i <= 90; i++) // A - Z
            {
                int_to_char_mapping.Add(total + (i - 65), (char)i);
                char_to_int_mapping.Add((char)i, total + (i - 65));
                Console.WriteLine("char: " + (char)i + "; value: " + (total + (i-65)));
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
        public static bool modify_canvas(SKCanvas canvas, List<List<CellData>> cellData, int row, int col, int cellSize)
        {
            if (canvas == null || row < 0 || col < 0) return false;

            // Check bounds based on list dimensions
            if (row >= cellData.Count || col >= cellData[0].Count) return false;

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
                    TextSize = 18,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center,
                    Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
                };

                // Cell position
                int x = col * cellSize;
                int y = row * cellSize;
                var rect = new SKRect(x, y, x + cellSize, y + cellSize);

                // Draw background and border
                canvas.DrawRect(rect, new SKPaint { Color = cell.background });
                canvas.DrawRect(rect, borderPaint);

                // Draw letter if present
                if (!string.IsNullOrEmpty(cell.letter))
                {
                    float textX = x + cellSize / 2f;
                    float textY = y + cellSize / 2f + textPaint.TextSize / 3f;
                    canvas.DrawText(cell.letter, textX, textY, textPaint);
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
        /// <param name="phrase_info">Word and position/orientation to draw.</param>
        /// <param name="canvas">The canvas to render on.</param>
        /// <param name="cellSize">Pixel size of each cell.</param>
        /// <param name="colors">Dictionary of color names to SKColor values.</param>
        static void draw_string(ref List<List<CellData>> cellData, phrase_struct phrase_info, SKCanvas canvas, int cellSize, Dictionary<string, SKColor> colors)
        {
            if (!draw_string_check(cellData, phrase_info))
            {
                Console.WriteLine("Cannot draw string: does not fit or cells occupied.");
                return;
            }
            
            for (int i = 0; i < phrase_info.phrase.Length; i++)
            {
                int r = phrase_info.row + (phrase_info.draw_right ? 0 : i);
                int c = phrase_info.col + (phrase_info.draw_right ? i : 0);

                string new_char = phrase_info.phrase[i].ToString();
                string existing_char = cellData[r][c].letter;

                // Determine if the resulting character should be uppercase
                bool should_be_upper = 
                    (!string.IsNullOrEmpty(new_char) && char.IsUpper(new_char[0])) ||
                    (!string.IsNullOrEmpty(existing_char) && char.IsUpper(existing_char[0]));
                string final_char = should_be_upper ? new_char.ToUpper() : new_char.ToLower();


                var cell = cellData[r][c]; // structs are passed by value so need to create a copy to modify
                cell.letter = final_char;
                cell.background = SKColors.LightGray;
                cell.color_name = "lightgray";
                cellData[r][c] = cell;

                modify_canvas(canvas, cellData, r, c, cellSize);
            }
        }
        
        /// <summary>
        /// Checks if a word can be placed on the grid without conflicts.
        /// </summary>
        /// <param name="cellData">The grid to check against.</param>
        /// <param name="phrase_info">Word and position/orientation to validate.</param>
        /// <returns>True if the word can be placed; otherwise, false.</returns>
        static bool draw_string_check(List<List<CellData>> cellData, phrase_struct phrase_info)
        {
            // Check bounds
            if (phrase_info.draw_right && phrase_info.col + phrase_info.phrase.Length > cellData[0].Count) return false;
            if (!phrase_info.draw_right && phrase_info.row + phrase_info.phrase.Length > cellData.Count) return false;

            for (int i = 0; i < phrase_info.phrase.Length; i++)
            {
                int r = phrase_info.row + (phrase_info.draw_right ? 0 : i);
                int c = phrase_info.col + (phrase_info.draw_right ? i : 0);

                if (!string.IsNullOrEmpty(cellData[r][c].letter) &&
                    cellData[r][c].letter != phrase_info.phrase[i].ToString())
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
        static bool initialize_grid(ref List<List<CellData>> cellData, int nrows, int ncols)
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
        public static void export_puzzle(SKBitmap bitmap, string filename)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(filename);
            data.SaveTo(stream);
            Console.WriteLine("Crossword saved as " +  filename);
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
        static (List<CellData> diag_neighbors, List<CellData> cross_neighbors) get_neighbors(List<List<CellData>> cellData, CellData cell)
        {
            // todo: modify to do somthing different if the cell passed in is default
            var diag_neighbors = new List<CellData>();
            var cross_neighbors = new List<CellData>();
            
            // All 8 neighbor offsets: diagonals and crosses
            (int row, int col)[] offsets = {
                (-1, -1), (-1, 0), (-1, 1),
                (0, -1),           (0, 1),
                (1, -1),  (1, 0),  (1, 1)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                int nr = cell.row + offsets[i].row;
                int nc = cell.col + offsets[i].col;

                bool inBounds = nr >= 0 && nr < cellData.Count && nc >= 0 && nc < cellData[0].Count;
                var neighbor = inBounds ? cellData[nr][nc] : new CellData(); // default if not in bounds
                
                if (offsets[i].row != 0 && offsets[i].col != 0) // Diagonal neighbor
                {
                    diag_neighbors.Add(string.IsNullOrEmpty(neighbor.letter) || neighbor.letter[0].Equals('=') ? new CellData() : neighbor);
                }
                else // Cross (non-diagonal) neighbor
                {
                    cross_neighbors.Add(string.IsNullOrEmpty(neighbor.letter) || neighbor.letter[0].Equals('=') ? new CellData() : neighbor);
                }
            }

            return (diag_neighbors, cross_neighbors);
        }
        
        /// <summary>
        /// Calculates the resulting color based on the target color and the current color state.
        /// </summary>
        /// <param name="target_color">The color intended to be applied.</param>
        /// <param name="current_color">The existing color state of the cell.</param>
        /// <returns>The new combined color as a string.</returns>
        static string calculate_color(string target_color, string current_color)
        {
            target_color = target_color.ToLower();
            current_color = current_color.ToLower();

            if (current_color == "lightgray")
            {
                return target_color;
            }
            else if (current_color == "red" && target_color != "red")
            {
                return current_color + "_" + target_color;
            }
            else if (current_color == "blue" && target_color != "blue")
            {
                if (target_color == "red")
                    return "red_blue";
                else if (target_color == "green")
                    return "blue_green";
            }
            else if (current_color == "green" && target_color != "green")
            {
                return target_color + "_" + current_color;
            }
            else if (current_color != "red" && current_color != "blue" && current_color != "green")
            {
                return "red_blue_green";
            }

            // fallback return original current_color if none of above matched
            return current_color;
        }
        
        /// <summary>
        /// Colors a given cell and its neighbors on the canvas according to the specified target color.
        /// </summary>
        /// <param name="cellData">The 2D list representing the puzzle grid cells.</param>
        /// <param name="canvas">The SKCanvas on which to draw.</param>
        /// <param name="target_color">The target color to apply ("red", "blue", or "green").</param>
        /// <param name="possible_index">The cell whose color and neighbors will be updated.</param>
        /// <param name="cellSize">The size of each cell on the canvas.</param>
        /// <returns>Returns true if the coloring was successful; false if the target color is invalid or a drawing error occurs.</returns>
        static bool color_limiter(List<List<CellData>> cellData, SKCanvas canvas, Dictionary<string, SKColor> colors, string target_color, CellData possible_index, int cellSize, Dictionary<string, object> language_dicts)
        {
            if (target_color != "red" && target_color != "blue" && target_color != "green")
                return false; // Invalid color, error

            SKColor darkColor;
            switch (target_color)
            {
                case "red": darkColor = colors["dark_red"]; break;
                case "blue": darkColor = colors["dark_blue"]; break;
                case "green": darkColor = colors["dark_green"]; break;
                default: return false; // Invalid color, error
            }

            CellData cell = cellData[possible_index.row][possible_index.col];
            cell.background = darkColor;
            cell.color_name = target_color;
            

            (List<CellData> diag_neighbors, List<CellData> cross_neighbors) = get_neighbors(cellData, possible_index);

            var valid_neighbor_positions = new List<(int row, int col)>();
            // Filter for valid neighbors
            switch (target_color)
            {
                case "red":
                    valid_neighbor_positions = cross_neighbors
                        .Where(n => !string.IsNullOrEmpty(n.letter) && !n.letter.StartsWith('='))
                        .Select(n => (n.row, n.col))
                        .ToList();
                    break;
                case "blue":
                    valid_neighbor_positions = diag_neighbors
                        .Where(n => !string.IsNullOrEmpty(n.letter) && !n.letter.StartsWith('='))
                        .Select(n => (n.row, n.col))
                        .ToList();
                    break;
                case "green":
                    valid_neighbor_positions = diag_neighbors.Concat(cross_neighbors).ToList()
                        .Where(n => !string.IsNullOrEmpty(n.letter) && !n.letter.StartsWith('='))
                        .Select(n => (n.row, n.col))
                        .ToList();
                    break;
            }

            // Assign limiter letter using calculate_limiter
            var limiter_value = calculate_limiter(valid_neighbor_positions, language_dicts, cellData);
            
            cell.letter = limiter_value;

            cellData[possible_index.row][possible_index.col] = cell;
            if (!modify_canvas(canvas, cellData, possible_index.row, possible_index.col, cellSize))
                return false;

            List<CellData> neighborsToColor;
            switch (target_color)
            {
                case "red": neighborsToColor = cross_neighbors; break;
                case "blue": neighborsToColor = diag_neighbors; break;
                case "green": neighborsToColor = diag_neighbors.Concat(cross_neighbors).ToList(); break;
                default: return false; // Invalid color, error
            }

            for (int i = 0; i < neighborsToColor.Count; i++)
            {
                if (!string.IsNullOrEmpty(neighborsToColor[i].letter))
                {
                    CellData neighbor = neighborsToColor[i];
                    cell = cellData[neighbor.row][neighbor.col];
                    cell.color_name = calculate_color(target_color, cell.color_name);
                    cell.background = colors[cell.color_name];
                    cellData[neighbor.row][neighbor.col] = cell;
                    if (!modify_canvas(canvas, cellData, neighbor.row, neighbor.col, cellSize))
                    {
                        Console.WriteLine("error");
                        return false;
                    }
                }
            }
            return true;
        }
        
        static bool create_limiter_2(List<List<CellData>> cell_data, SKCanvas canvas, Dictionary<string, SKColor> colors, int cell_size, Dictionary<string, object> language_dicts)
        {
            int nrows = cell_data.Count;
            int ncols = cell_data[0].Count;
            List<CellData> possible_limiters = new();
            string[] base_colors = ["red", "blue", "green"];
            Random rand = new();
            string random_color = base_colors[rand.Next(3)];
            bool has_valid_neighbor = false;

            for (int r = 0; r < nrows; r++)
            {
                for (int c = 0; c < ncols; c++)
                {
                    CellData currentCell = cell_data[r][c];
                    bool isEmpty = string.IsNullOrEmpty(currentCell.letter);
                    var (diag_neighbors, cross_neighbors) = isEmpty ?
                        get_neighbors(cell_data, currentCell) :
                        (new List<CellData> { new(), new(), new(), new() }, new List<CellData> { new(), new(), new(), new() });
                    var all_neighbors = diag_neighbors.Concat(cross_neighbors);
                    
                    
                    switch (random_color)
                    {
                        case "red":
                            has_valid_neighbor = cross_neighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.letter) &&
                                !cell.letter.StartsWith("="));
                            break;
                        case "blue":
                            has_valid_neighbor = diag_neighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.letter) &&
                                !cell.letter.StartsWith("="));
                            break;
                        case "green":
                            has_valid_neighbor = all_neighbors.Any(cell =>
                                !string.IsNullOrEmpty(cell.letter) &&
                                !cell.letter.StartsWith("="));
                            break;
                    }


                    if (has_valid_neighbor)
                    {
                        possible_limiters.Add(cell_data[r][c]);
                    }
                }
            }

            if (possible_limiters.Count == 0)
                return false;

            possible_limiters = possible_limiters.OrderBy(_ => rand.Next()).ToList();

            // Pass first possible limiter to color_limiter with "red"
            return color_limiter(cell_data, canvas, colors, random_color, possible_limiters[0], cell_size, language_dicts);
        }

        
        /// <summary>
        /// Resets key variables to their initial state for puzzle generation.
        /// </summary>
        /// <param name="cellData">The crossword grid to be cleared and reset.</param>
        /// <param name="inputs_added">The list of inputs that have already been added to the grid.</param>
        public static void reset_variables(ref List<List<CellData>> cellData, ref List<string> inputs_added)
        {
            cellData = new List<List<CellData>>();
            inputs_added = new List<string>();
        }

        /// <summary>
        /// Counts how many strings in a given list fully match the characters at specific positions in a target string.
        /// </summary>
        /// <param name="targetString">The string against which candidate strings are compared.</param>
        /// <param name="positions">A list of indices in the string to check for matching characters.</param>
        /// <param name="candidates">A list of candidate strings to check for matching patterns.</param>
        /// <returns>
        /// The number of candidate strings whose characters at all specified positions exactly match those in the target string.
        /// </returns>
        static bool IsLimiterValidForDifficulty(
            List<string> input_names,
            string baseWord,
            List<int> constrainedPositions,
            string difficulty)
        {
            int requiredOverlap = difficulty.ToLower() switch
            {
                "beginner" => 0,
                "novice" => 1,
                "intermediate" => 2,
                "expert" => 3,
                "master" => 4,
                _ => 0
            };

            // Filter input_names by length of baseWord
            var filteredCandidates = input_names.Where(candidate => candidate.Length == baseWord.Length);

            int matchCount = 0;

            foreach (var candidate in filteredCandidates)
            {
                bool matches = true;

                foreach (var pos in constrainedPositions)
                {
                    if (candidate[pos] != baseWord[pos])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches) matchCount++;

                if (matchCount > requiredOverlap)
                    return true; // Enough overlap found, valid limiter
            }

            return requiredOverlap == 0;
        }

    }
}
