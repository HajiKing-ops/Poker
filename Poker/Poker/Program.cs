using System;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Poker
{
    class Program
    {
        static readonly Random rand = new Random(); // global object so it can be used everywhere

        // -----------------------
        // DECLARATION DES DONNEES
        // -----------------------
        // Importation des DL (librairies de code) permettant de gérer les couleurs en mode console
        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, int wAttributes);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetStdHandle(uint nStdHandle);
        static uint STD_OUTPUT_HANDLE = 0xfffffff5;
        static IntPtr hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        // Pour utiliser la fonction C 'getchar()' : sasie d'un caractère
        [DllImport("msvcrt")]
        static extern int _getch();

        //-------------------
        // TYPES DE DONNEES
        //-------------------

        // Fin du jeu
        public static bool fin = false;

        // Codes COULEUR
        public enum couleur { VERT = 10, ROUGE = 12, JAUNE = 14, BLANC = 15, NOIRE = 0, ROUGESURBLANC = 252, NOIRESURBLANC = 240 };

        // Coordonnées pour l'affichage
        public struct coordonnees
        {
            public int x;
            public int y;
        }

        // Une carte
        public struct carte
        {
            public char valeur;
            public int famille;
        }

        // Liste des combinaisons possibles
        public enum combinaison { RIEN, PAIRE, DOUBLE_PAIRE, BRELAN, QUINTE, FULL, COULEUR, CARRE, QUINTE_FLUSH };

        // Valeurs des cartes : As, Roi,...
        public static char[] valeurs = { 'A', 'R', 'D', 'V', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };

        // Codes ASCII (3 : coeur, 4 : carreau, 5 : trèfle, 6 : pique)
        public static char[] familles = { '\u2665', '\u2666', '\u2663', '\u2660' };

        // Numéros des cartes à échanger
        public static int[] echange = { 0, 0, 0, 0 };

        // Jeu de 5 cartes
        public static carte[] MonJeu = new carte[5];

        //----------
        // FONCTIONS
        //----------

        // Génère aléatoirement une carte : {valeur;famille}
        // Retourne une expression de type "structure carte"
        public static carte tirage()
        {
            carte uneCarte = new carte(); // / Initialise une carte vide avant affectation.


            int v = rand.Next(0, 13);
            int f = rand.Next(0, 4);
            uneCarte.valeur = valeurs[v];
            uneCarte.famille = familles[f];  // Assign the values from arrays to the carte object's properties
            return uneCarte;
        }

        // Indique si une carte est déjà présente dans le jeu
        // Paramètres : une carte, le jeu 5 cartes, le numéro de la carte dans le jeu
        // Retourne un entier (booléen)
        public static bool carteUnique(carte uneCarte, carte[] unJeu, int numero)  // numero = index de la carte courante (on ne la compare pas à elle-même).
        {
            for (int i = 0; i < 5; i++)
            {
                if (i == numero) continue; // skip current iteration, we don't want to compare the card with itself
                {
                    if (uneCarte.valeur == unJeu[i].valeur && uneCarte.famille == unJeu[i].famille)
                    { return false; }
                }
            }
            return true;
        }

        // Calcule et retourne la COMBINAISON (paire, double-paire... , quinte-flush)
        // pour un jeu complet de 5 cartes.
        // La valeur retournée est un élement de l'énumération 'combinaison' (=constante)
        public static combinaison Cherche_combinaison(ref carte[] unJeu)
        {
            int i;
            int j;
            int compt = 0;
            int[] similaire = { 0, 0, 0, 0, 0, };

            combinaison comb = combinaison.RIEN;  // Valeur par défaut: aucune combinaison trouvée.
            //combinaison combine = new combinaison();
            int compteur = 0;
            bool hasbrelan = false;
            bool haspaire = false;
            bool toutmemefam = true;

            for (i = 0; i < 5; i++)
            {
                for (j = 0; j < 5; j++) // pour list simple on peut mettre length 
                {
                    //verif paire 
                    if (unJeu[i].valeur == unJeu[j].valeur)
                    {
                        similaire[i] += 1;


                    }
                    if (unJeu[i].famille == unJeu[j].famille)
                    { compt += 1; }

                }
                if (similaire[i] == 2)
                {
                    compteur += 1;
                    comb = combinaison.PAIRE; // On continue l’analyse: une meilleure combinaison peut exister.
                }
                if (compteur / 2 == 2) /// i did this to find the double paire  {1, 4, 4, 4, 4} = 2 paire si on fait ca /2
                { comb = combinaison.DOUBLE_PAIRE; } // we had list of combinasion at the top so and i call combinasion and the value in it 
                if (similaire[i] == 4)
                { comb = combinaison.CARRE; } // si il y a un elemenet qui aparait 4 fois il va affiche ca 
                if (similaire[i] == 3)
                { comb = combinaison.BRELAN; }
                //if (similaire[i] == 3 && similaire[i] == 2)
                //{ comb =  combinaison.FULL; }

                if (similaire[i] == 3)
                    hasbrelan = true;
                if (similaire[i] == 2)
                    haspaire = true;

            }

            for (int k = 1; k < 5; k++)
            {
                if (unJeu[k].famille != unJeu[0].famille) // if one card has a different suit, it's not a flush
                    toutmemefam = false; // Dès qu'une carte diffère, ce n'est pas une couleur
            }
            if (hasbrelan && haspaire)  // FULL = brelan + paire dans le même jeu
                comb = combinaison.FULL;
            if (toutmemefam) // COULEUR = toutes les cartes ont la même famille
                comb = combinaison.COULEUR;



            char[,] quintes =
            {
                { 'X','V','D','R','A'},
                { '9','X','V','D','R'},
                { '8','9','X','V','D'},
                { '7','8','9','X','V'}, 
                { '6','7','8','9','X'},
            };
            bool isquint = false;
            bool isquintflush = false;

            for (i = 0; i < quintes.GetLength(0); i++)
            {
                int matchcount = 0;
                for (j = 0; j < 5; j++) //  you can use Getlength(1) here instead of 5, -> 0 = first dimension, 1 -> second dimension, it adapts automatically 
                {
                    for (int k = 0; k < 5; k++)
                    {
                        if (unJeu[j].valeur == quintes[i, k]) // unjeu is my hands card, and 2D array you use two indexes (row, column)
                        {
                            matchcount++;
                            break; //match found break k loop automatically j increment to 1 ... 
                        }
                    }
                }
                if (matchcount == 5)
                {
                    isquint = true;
                    if (toutmemefam)
                    {
                        isquintflush = true;
                    }
                    break;
                }
            }
            if (isquintflush)
                comb = combinaison.QUINTE_FLUSH;
            else if (isquint)
                comb = combinaison.QUINTE;
            return comb;
        }

        // Echange des cartes
        // Paramètres : le tableau de 5 cartes et le tableau des numéros des cartes à échanger
        private static void echangeCarte(ref carte[] unJeu, ref int[] e)
        {


            for (int i = 0; i < e.Length; i++)
            {
                int g = e[i];
                do
                {
                    unJeu[g] = tirage();
                } while (!carteUnique(unJeu[e[i]], unJeu, g)); // passes 3 parameters to carteUnique to check for duplicates
            }
        }

        // Tirage d'un jeu de 5 cartes
        // Paramètre : le tableau de 5 cartes à remplir
        private static void tirageDuJeu(ref carte[] unJeu)
        {

            for (int i = 0; i < 5; i++)
            {
                do
                {
                    unJeu[i] = tirage();
                }
                while (!carteUnique(unJeu[i], unJeu, i)); // same logic as echangeCarte, retires until unique
            }

        }
        private static void affichageCarte(ref carte uneCarte)
        {

            int left = 0;
            int c = 1;

            for (int i = 0; i < 5; i++)
            {

                // Affichage de la carte
                // Coeur/Carreau en rouge, sinon noir (fond blanc).
                if (MonJeu[i].famille == '\u2665' || MonJeu[i].famille == '\u2666')    // \u2666 are unicode characters -> heart,diamond so it the symbol 
                    SetConsoleTextAttribute(hConsole, 252);
                else
                    SetConsoleTextAttribute(hConsole, 240);
                Console.SetCursorPosition(left, 5);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '*', '-', '-', '-', '-', '-', '-', '-', '-', '-', '*');
                Console.SetCursorPosition(left, 6);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, '|');
                Console.SetCursorPosition(left, 7);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '|');
                Console.SetCursorPosition(left, 8);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', (char)MonJeu[i].famille, ' ', ' ', ' ', ' ', ' ', ' ', ' ', (char)MonJeu[i].famille, '|');
                Console.SetCursorPosition(left, 9);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', ' ', (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, ' ', ' ', ' ', '|');
                Console.SetCursorPosition(left, 10);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', (char)MonJeu[i].famille, ' ', ' ', (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, ' ', ' ', (char)MonJeu[i].famille, '|');
                Console.SetCursorPosition(left, 11);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', ' ', (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, (char)MonJeu[i].valeur, ' ', ' ', ' ', '|');
                Console.SetCursorPosition(left, 12);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', (char)MonJeu[i].famille, ' ', ' ', ' ', ' ', ' ', ' ', ' ', (char)MonJeu[i].famille, '|');
                Console.SetCursorPosition(left, 13);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '|');
                Console.SetCursorPosition(left, 14);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, ' ', (char)MonJeu[i].famille, '|');
                Console.SetCursorPosition(left, 15);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '*', '-', '-', '-', '-', '-', '-', '-', '-', '-', '*');
                Console.SetCursorPosition(left, 16);
                SetConsoleTextAttribute(hConsole, 10);
                Console.Write("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", ' ', ' ', ' ', ' ', ' ', c, ' ', ' ', ' ', ' ', ' ');
                left = left + 15; // Décale la position horizontale pour la prochaine carte
                c++;
            }

        }





        public static string Encrypt(string text)
        {
            string res = "";

            for (int i = 0; i < text.Length; i++)
            {
                res += (char)(text[i] + 3); // Chiffrement César: décale chaque caractère de +3.
            }
            return res; 
        }
        public static string Decrypt(string text)
        {
            string res = "";

            for (int i = 0; i < text.Length; i++)
            {
                res += (char)(text[i] - 3); // Chiffrement César: décale chaque caractère de -3.
            }
            return res;
        }
        //--------------------`
        // Fonction PRINCIPALE
        //--------------------
        static void Main(string[] args)
        {
            //---------------
            // BOUCLE DU JEU
            //---------------
            string reponse;

            Console.OutputEncoding = Encoding.GetEncoding(65001);

            SetConsoleTextAttribute(hConsole, 012);
            while (true)
            {
                // Positionnement et affichage
                Console.Clear();
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '*', '-', '-', '-', '-', '-', '-', '-', '-', '-', '*');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', 'P', 'O', 'K', 'E', 'R', ' ', ' ', '|');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', '|');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', '1', ' ', 'J', 'o', 'u', 'e', 'r', ' ', '|');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', '2', ' ', 'S', 'c', 'o', 'r', 'e', ' ', '|');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '|', ' ', '3', ' ', 'F', 'i', 'n', ' ', ' ', ' ', '|');
                Console.WriteLine("{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}{10}\n", '*', '-', '-', '-', '-', '-', '-', '-', '-', '-', '*');
                Console.WriteLine();
                // Lecture du choix


                do
                {
                    SetConsoleTextAttribute(hConsole, 014);
                    Console.Write("Votre choix : ");
                    reponse = Console.ReadLine();
                }
                while (reponse != "1" && reponse != "2" && reponse != "3");
                Console.Clear();
                SetConsoleTextAttribute(hConsole, 015);
                // Jouer au Poker
                if (reponse == "1")
                {
                    int i = 0;
                    tirageDuJeu(ref MonJeu);
                    affichageCarte(ref MonJeu[i]);

                    // Nombre de carte à échanger
                    try
                    {
                        int compteur = 0;
                        SetConsoleTextAttribute(hConsole, 012);
                        Console.Write("Nombre de cartes a echanger <0-5> ? : ");
                        compteur = int.Parse(Console.ReadLine());
                        int[] e = new int[compteur];
                        for (int j = 0; j < e.Length; j++)
                        {
                            Console.Write("Carte <1-5> : ");

                            e[j] = int.Parse(Console.ReadLine());
                            e[j] -= 1; // Convertit le numéro affiché (1-5) en index tableau (0-4)
                        }

                        echangeCarte(ref MonJeu, ref e);

                    }
                    catch { }
                    //---------------------------------------
                    // CALCUL ET AFFICHAGE DU RESULTAT DU JEU
                    //---------------------------------------
                    Console.Clear();
                    affichageCarte(ref MonJeu[i]);
                    SetConsoleTextAttribute(hConsole, 012);
                    Console.Write("RESULTAT - Vous avez : ");
                    try
                    {
                        // Test de la combinaison
                        switch (Cherche_combinaison(ref MonJeu))
                        {
                            case combinaison.RIEN:
                                Console.WriteLine("rien du tout... desole!"); break;
                            case combinaison.PAIRE:
                                Console.WriteLine("une simple paire..."); break;
                            case combinaison.DOUBLE_PAIRE:
                                Console.WriteLine("une double paire; on peut esperer..."); break;
                            case combinaison.BRELAN:
                                Console.WriteLine("un brelan; pas mal..."); break;
                            case combinaison.QUINTE:
                                Console.WriteLine("une quinte; bien!"); break;
                            case combinaison.FULL:
                                Console.WriteLine("un full; ouahh!"); break;
                            case combinaison.COULEUR:
                                Console.WriteLine("une couleur; bravo!"); break;
                            case combinaison.CARRE:
                                Console.WriteLine("un carre; champion!"); break;
                            case combinaison.QUINTE_FLUSH:
                                Console.WriteLine("une quinte-flush; royal!"); break;
                        }
                        ;
                    }
                    catch { }
                    Console.ReadKey();
                    char enregister = ' ';
                    string nom = "";
                    //BinaryWriter f;
                    SetConsoleTextAttribute(hConsole, 014);
                    Console.Write("Enregistrer le Jeu ? (O/N) : ");
                    try
                    {
                        enregister = char.Parse(Console.ReadLine());
                        enregister = Char.ToUpper(enregister);  // Normalise en majuscule pour comparer avec 'O'
                    }
                    catch (Exception e) { Console.WriteLine(e.Message); }


                    if (enregister == 'O') 
                    {
                        Console.WriteLine("Vous pouvez saisir votre nom (ou pseudo) : ");
                        nom = Console.ReadLine();
                        string[] card = new string[5];  
                        for (int j = 0; j < 5; j++)
                        {
                            if (MonJeu[j].famille == '\u2665')
                                card[j] += "3," + MonJeu[j].valeur;
                            if (MonJeu[j].famille == '\u2666')
                                card[j] += "4," + MonJeu[j].valeur;
                            if (MonJeu[j].famille == '\u2663')
                                card[j] += "5," + MonJeu[j].valeur;
                            if (MonJeu[j].famille == '\u2660')
                                card[j] += "6," + MonJeu[j].valeur;
 
                        }   // each card saved as family code,value

                        using (StreamWriter f = new StreamWriter("scores.txt", true))
                        {
                            f.WriteLine(Encrypt(nom + " _ " + Cherche_combinaison(ref MonJeu).ToString()));  // Écrit le nom + combinaison ET les cartes chiffrées dans scores.txt (mode ajout)
                            f.WriteLine(Encrypt(string.Join(";", card)));  // joins all 5 cards with ';' separator
                        }
                        Console.WriteLine("Score enregistre!");
                        Console.ReadKey();
                    }

                }
                if (reponse == "2")
                {
                    if (File.Exists("scores.txt"))
                    {
                        SetConsoleTextAttribute(hConsole, 014);
                        Console.WriteLine("========SCORES======\n");

                        string[] lines = File.ReadAllLines("scores.txt");
                        foreach (string line in lines) 
                        {
                            string decrypted = Decrypt(line);
                            if (decrypted.Contains(",")) // check if the line contains a comma (card data line)
                            {
                                string[] parts = decrypted.Split(';'); // split into 5 cards using ';' as separator
                                for (int i = 0; i < 5; i++)
                                {

                                    string[] cardData = parts[i].Split(','); // split each card into family code and value

                                    MonJeu[i].valeur = char.Parse(cardData[1]); // store the card value

                                    if (cardData[0] == "3")
                                        MonJeu[i].famille = '\u2665';
                                    if (cardData[0] == "4")
                                        MonJeu[i].famille = '\u2666';
                                    if (cardData[0] == "5")
                                        MonJeu[i].famille = '\u2663';
                                    if (cardData[0] == "6")
                                        MonJeu[i].famille = '\u2660';

                                    Console.WriteLine();//new line after each line 
                                    Console.WriteLine();//new line after each line 
                                    Console.WriteLine();//new line after each line   
                                    Console.WriteLine();//new line after each line 
                                    Console.WriteLine();//new line after each line
                                }
                                affichageCarte(ref MonJeu[0]);

                            }
                            else
                            {
                                Console.WriteLine(decrypted);
                            }

                        }
                        Console.WriteLine("\n APPuyez sur une touche ");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Ps de score enregistre : ");
                        Console.ReadKey();
                    }

                }

                if (reponse == "3")
                    break;

            }

            Console.Clear();
            Console.ReadKey();
        }
    }
}
