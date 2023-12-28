// 20010011055 Mehmet Seyfullah Özen

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq; //Verileri sorgulamaya yarýyor.
using System.Windows.Forms;

namespace VM_Proje
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent(); 
        }

        OleDbConnection baglanti = new OleDbConnection(@"Provider=Microsoft.ACE.OLEDB.12.0;
            Data Source=C:\Users\Seyfullah\source\repos\VM_Proje\happydata.xlsx;
            Extended Properties='Excel 12.0 Xml;HDR=YES;'");

        void Veriler()
        {
            OleDbDataAdapter da = new OleDbDataAdapter("Select * from [happydata$]", baglanti); 
            DataTable dt = new DataTable();
            da.Fill(dt);
            dataGridView1.DataSource = dt; 
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Veriler();
        }

        private void btnEksikVeriIsleme_Click(object sender, EventArgs e)
        {
            EksikVeriTamamlama();
        }

        private void EksikVeriTamamlama()
        {
            DataTable dt = ((DataTable)dataGridView1.DataSource).Copy(); 

            for (int colIndex = 0; colIndex < dt.Columns.Count; colIndex++)
            {
                if (dt.Columns[colIndex].DataType == typeof(double))
                {
                    double medyan = HesaplaMedyan(dt, colIndex);

                    for (int rowIndex = 0; rowIndex < dt.Rows.Count; rowIndex++)
                    {
                        if (dt.Rows[rowIndex][colIndex] == DBNull.Value)
                        {
                            dt.Rows[rowIndex][colIndex] = medyan;
                        }
                    }
                }
            }
            dataGridView2.DataSource = dt;
        }

        private double HesaplaMedyan(DataTable dt, int colIndex)
        {
            List<double> veriler = new List<double>();

            foreach (DataRow row in dt.Rows)
            {
                if (row[colIndex] != DBNull.Value)
                {
                    veriler.Add(Convert.ToDouble(row[colIndex]));
                }
            }

            int n = veriler.Count;
            if (n % 2 == 0)
            {
                return (veriler[n / 2] + veriler[(n / 2) + 1]) / 2.0;
            }
            else
            {
                return veriler[n / 2];
            }
        }

        private void btnSiniflandirma_Click(object sender, EventArgs e)
        {
            DataTable dt = ((DataTable)dataGridView2.DataSource).Copy();
            DataTable sonucTable = new DataTable();
            int k = 10;
            List<DataTable> folds = KFoldCrossValidation(dt, k);

            sonucTable.Columns.Add("happy", typeof(double));
            sonucTable.Columns.Add("Tahmin", typeof(double));

            for (int i = 0; i < k; i++)
            {
                DataTable testFold = folds[i];
                DataTable trainFolds = folds.Where((_, index) => index != i).SelectMany(x => x.AsEnumerable()).CopyToDataTable();

                Dictionary<double, Dictionary<string, double>> model = TrainNaiveBayes(trainFolds);

                List<double> predictions = PredictNaiveBayes(testFold, model);

                for (int rowIndex = 0; rowIndex < testFold.Rows.Count; rowIndex++)
                {
                    DataRow resultRow = sonucTable.NewRow();
                    resultRow["happy"] = dt.Rows[rowIndex]["happy"]; 
                    resultRow["Tahmin"] = predictions[rowIndex];
                    sonucTable.Rows.Add(resultRow);
                }
            }
            dataGridView4.DataSource = sonucTable;
        }

        private Dictionary<double, Dictionary<string, double>> TrainNaiveBayes(DataTable data)
        {
            Dictionary<double, Dictionary<string, double>> model = new Dictionary<double, Dictionary<string, double>>();

            List<double> classes = data.AsEnumerable().Select(r => r.Field<double>("happy")).Distinct().ToList();

            foreach (double c in classes)
            {
                Dictionary<string, double> classProbabilities = new Dictionary<string, double>();

                foreach (DataColumn column in data.Columns)
                {
                    if (column.DataType == typeof(double) && column.ColumnName != "happy")
                    {
                        double mean = data.AsEnumerable().Where(r => r.Field<double>("happy") == c).Select(r => r.Field<double>(column)).Average();
                        double stdDev = CalculateStandardDeviation(data.AsEnumerable().Where(r => r.Field<double>("happy") == c).Select(r => r.Field<double>(column)));
                        classProbabilities[column.ColumnName] = mean;
                        classProbabilities[column.ColumnName + "_stdDev"] = stdDev;
                    }
                }
                double classProbability = (double)data.AsEnumerable().Where(r => r.Field<double>("happy") == c).Count() / data.Rows.Count;

                model[c] = classProbabilities;
                model[c]["ClassProbability"] = classProbability;
            }

            return model;
        }

        private List<double> PredictNaiveBayes(DataTable testSet, Dictionary<double, Dictionary<string, double>> model)
        {
            List<double> predictions = new List<double>();

            foreach (DataRow row in testSet.Rows)
            {
                double bestClass = double.MinValue;
                double bestClassProbability = double.MinValue;

                foreach (double c in model.Keys)
                {
                    double classProbability = Math.Log(model[c]["ClassProbability"]);

                    foreach (DataColumn column in testSet.Columns)
                    {
                        if (column.DataType == typeof(double) && column.ColumnName != "happy")
                        {
                            double x = Convert.ToDouble(row[column.ColumnName]);
                            double mean = model[c][column.ColumnName];
                            double stdDev = model[c][column.ColumnName + "_stdDev"];

                            double exponent = Math.Exp(-(Math.Pow(x - mean, 2) / (2 * Math.Pow(stdDev, 2))));
                            double conditionalProbability = (1 / (Math.Sqrt(2 * Math.PI) * stdDev)) * exponent;

                            classProbability += Math.Log(conditionalProbability);
                        }
                    }
                    if (classProbability > bestClassProbability || bestClassProbability == double.MinValue)
                    {
                        bestClass = c;
                        bestClassProbability = classProbability;
                    }
                }
                predictions.Add(bestClass);
            }
            return predictions;
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            double mean = values.Average();
            double sumOfSquares = values.Select(val => Math.Pow(val - mean, 2)).Sum();
            return Math.Sqrt(sumOfSquares / values.Count());
        }

        private void btnNormalizasyon_Click(object sender, EventArgs e)
        {
            double[] newMinValues = Enumerable.Repeat(0.0, dataGridView2.Columns.Count).ToArray();
            double[] newMaxValues = Enumerable.Repeat(1.0, dataGridView2.Columns.Count).ToArray();
            DataTable dt = ((DataTable)dataGridView2.DataSource).Copy(); 
            for (int colIndex = 0; colIndex < dt.Columns.Count; colIndex++)
            {
                double columnMin = double.MaxValue;
                double columnMax = double.MinValue;

                foreach (DataRow row in dt.Rows)
                {
                    if (row[colIndex] != DBNull.Value)
                    {
                        double value = Convert.ToDouble(row[colIndex]);

                        if (value < columnMin)
                            columnMin = value;

                        if (value > columnMax)
                            columnMax = value;
                    }
                }
                for (int rowIndex = 0; rowIndex < dt.Rows.Count; rowIndex++)
                {
                    if (dt.Rows[rowIndex][colIndex] != DBNull.Value)
                    {
                        double originalValue = Convert.ToDouble(dt.Rows[rowIndex][colIndex]);
                        double normalizedValue = ((originalValue - columnMin) / (columnMax - columnMin)) * (newMaxValues[colIndex] - newMinValues[colIndex]) + newMinValues[colIndex];
                        
                        dt.Rows[rowIndex][colIndex] = normalizedValue;
                    }
                }
            }
            dataGridView3.DataSource = dt;
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void btnNormSiniflandirma_Click(object sender, EventArgs e)
        {
            DataTable dt = ((DataTable)dataGridView3.DataSource).Copy();
            int k = 10;
            List<DataTable> folds = KFoldCrossValidation(dt, k);

            DataTable sonucTable = new DataTable();
            sonucTable.Columns.Add("happy", typeof(double));
            sonucTable.Columns.Add("Tahmin", typeof(double));

            for (int i = 0; i < k; i++)
            {
                DataTable testFold = folds[i];
                DataTable trainFolds = folds.Where((_, index) => index != i).SelectMany(x => x.AsEnumerable()).CopyToDataTable();

                Dictionary<double, Dictionary<string, double>> model = TrainNaiveBayes(trainFolds);

                List<double> predictions = PredictNaiveBayes(testFold, model);

                DataTable currentSonucTable = new DataTable();
                currentSonucTable.Columns.Add("happy", typeof(double));
                currentSonucTable.Columns.Add("Tahmin", typeof(double));

                for (int rowIndex = 0; rowIndex < testFold.Rows.Count; rowIndex++)
                {
                    DataRow resultRow = currentSonucTable.NewRow();
                    resultRow["happy"] = dt.Rows[rowIndex]["happy"];
                    resultRow["Tahmin"] = predictions[rowIndex];
                    currentSonucTable.Rows.Add(resultRow);
                }
                sonucTable.Merge(currentSonucTable);
            }

            dataGridView5.DataSource = sonucTable;
        }
        private List<DataTable> KFoldCrossValidation(DataTable data, int k)
        {
            List<DataTable> folds = new List<DataTable>();

            Random rand = new Random();

            foreach (DataRow row in data.Rows)
            {
                int foldIndex = rand.Next(0, k);
                while (foldIndex >= folds.Count)
                {
                    folds.Add(data.Clone());
                }
                folds[foldIndex].ImportRow(row);
            }

            return folds;
        }

        private void btnNormHata_Click(object sender, EventArgs e)
        {
            DataTable dt = ((DataTable)dataGridView5.DataSource).Copy();
            DataColumn realColumn = dt.Columns["happy"];
            DataColumn predictedColumn = dt.Columns["Tahmin"];

            double sumSquaredError = 0;
            int rowCount = dt.Rows.Count;
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                double realValue = Convert.ToDouble(dt.Rows[rowIndex][realColumn]);
                double predictedValue = Convert.ToDouble(dt.Rows[rowIndex][predictedColumn]);
                double squaredError = Math.Pow(realValue - predictedValue, 2);
                sumSquaredError += squaredError;
            }
            double meanSquaredError = sumSquaredError / rowCount;

            DataTable resultTable = new DataTable();
            resultTable.Columns.Add("MSE", typeof(double));
            DataRow resultRow = resultTable.NewRow();
            resultRow["MSE"] = meanSquaredError;
            resultTable.Rows.Add(resultRow);
            dataGridView7.DataSource = resultTable;
        }

        private void btnOrjHata_Click(object sender, EventArgs e)
        {
            DataTable dt = ((DataTable)dataGridView4.DataSource).Copy();

            DataColumn realColumn = dt.Columns["happy"];
            DataColumn predictedColumn = dt.Columns["Tahmin"];

            double sumSquaredError = 0;
            int rowCount = dt.Rows.Count;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                double realValue = Convert.ToDouble(dt.Rows[rowIndex][realColumn]);
                double predictedValue = Convert.ToDouble(dt.Rows[rowIndex][predictedColumn]);
                double squaredError = Math.Pow(realValue - predictedValue, 2);
                sumSquaredError += squaredError;
            }

            double meanSquaredError = sumSquaredError / rowCount;

            DataTable resultTable = new DataTable();
            resultTable.Columns.Add("MSE", typeof(double));
            DataRow resultRow = resultTable.NewRow();
            resultRow["MSE"] = meanSquaredError;
            resultTable.Rows.Add(resultRow);
            dataGridView6.DataSource = resultTable;
        }
    }
}
