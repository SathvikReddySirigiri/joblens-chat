using System.Text.RegularExpressions;

namespace JobLens.Domain.Skills;

public sealed record SkillDefinition(string Canonical, IReadOnlyList<string> Aliases);

public static partial class SkillCatalog
{
    public static readonly IReadOnlyList<SkillDefinition> All =
    [
        Skill("SQL", "T-SQL", "PostgreSQL", "MySQL", "SQLite"),
        Skill("Python", "Django", "Flask", "FastAPI"),
        Skill("C#", "CSharp", "C Sharp"),
        Skill(".NET", "ASP.NET", "ASP.NET Core", "dotnet", ".NET Core"),
        Skill("Java", "Spring Boot", "JVM"),
        Skill("JavaScript", "JS", "ECMAScript"),
        Skill("TypeScript", "TS"),
        Skill("React", "React.js", "ReactJS"),
        Skill("Node.js", "Node", "Express.js"),
        Skill("HTML", "HTML5"),
        Skill("CSS", "CSS3", "Sass", "SCSS"),
        Skill("Power BI", "PowerBI"),
        Skill("Tableau"),
        Skill("Excel", "Microsoft Excel", "spreadsheets"),
        Skill("R", "RStudio"),
        Skill("Azure", "Microsoft Azure"),
        Skill("AWS", "Amazon Web Services", "EC2", "S3"),
        Skill("Google Cloud", "GCP", "BigQuery"),
        Skill("Machine Learning", "ML", "scikit-learn", "sklearn"),
        Skill("Deep Learning", "neural networks", "TensorFlow", "PyTorch"),
        Skill("Statistics", "statistical analysis", "hypothesis testing"),
        Skill("Data Visualization", "data viz", "dashboards"),
        Skill("Data Analysis", "data analytics", "analytics"),
        Skill("Data Engineering", "ETL", "data pipelines"),
        Skill("Data Science"),
        Skill("Pandas"),
        Skill("NumPy"),
        Skill("Spark", "Apache Spark", "PySpark"),
        Skill("Hadoop"),
        Skill("Kafka", "Apache Kafka"),
        Skill("Docker"),
        Skill("Kubernetes", "K8s"),
        Skill("Git", "GitHub", "GitLab", "version control"),
        Skill("Linux", "Unix", "Bash"),
        Skill("REST APIs", "REST", "RESTful", "Web APIs"),
        Skill("GraphQL"),
        Skill("Microservices"),
        Skill("Agile", "Scrum", "Kanban"),
        Skill("Communication", "written communication", "verbal communication"),
        Skill("Leadership", "team lead"),
        Skill("Teamwork", "collaboration"),
        Skill("Problem Solving", "critical thinking"),
        Skill("Project Management", "PMP", "project planning"),
        Skill("Customer Service", "client facing"),
        Skill("Public Speaking", "presentations"),
        Skill("Technical Writing", "documentation"),
        Skill("Salesforce"),
        Skill("SAP"),
        Skill("Oracle"),
        Skill("MongoDB", "NoSQL"),
        Skill("Redis"),
        Skill("Elasticsearch"),
        Skill("CI/CD", "continuous integration", "continuous delivery", "Jenkins", "GitHub Actions"),
        Skill("Terraform", "Infrastructure as Code", "IaC"),
        Skill("Ansible"),
        Skill("Cybersecurity", "information security", "InfoSec"),
        Skill("Networking", "TCP/IP", "network administration"),
        Skill("PowerShell"),
        Skill("VBA"),
        Skill("MATLAB"),
        Skill("SAS"),
        Skill("SPSS"),
        Skill("Stata"),
        Skill("Looker"),
        Skill("Snowflake"),
        Skill("Databricks"),
        Skill("Airflow", "Apache Airflow"),
        Skill("dbt"),
        Skill("Figma"),
        Skill("UI/UX", "user experience", "user interface"),
        Skill("Product Management"),
        Skill("Business Analysis", "requirements gathering"),
        Skill("Accounting", "bookkeeping"),
        Skill("Financial Modeling", "valuation"),
        Skill("Excel VBA"),
        Skill("A/B Testing", "experimentation"),
        Skill("NLP", "natural language processing"),
        Skill("Computer Vision"),
        Skill("LLM", "large language models", "Generative AI", "ChatGPT"),
        Skill("Prompt Engineering"),
        Skill("SEO"),
        Skill("Digital Marketing", "content marketing"),
        Skill("SQL Server", "MSSQL"),
        Skill("Entity Framework", "EF Core"),
        Skill("Blazor"),
        Skill("MVC", "Model-View-Controller"),
        Skill("Unit Testing", "xUnit", "NUnit", "Jest"),
        Skill("Go", "Golang"),
        Skill("C++"),
        Skill("Swift"),
        Skill("Kotlin"),
        Skill("Android"),
        Skill("iOS"),
        Skill("Flutter"),
        Skill("React Native"),
    ];

    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var found = new List<string>();
        foreach (var skill in All)
        {
            if (Matches(text, skill))
            {
                found.Add(skill.Canonical);
            }
        }

        return found;
    }

    public static bool Matches(string text, SkillDefinition skill)
    {
        foreach (var term in Terms(skill))
        {
            if (ContainsTerm(text, term))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Terms(SkillDefinition skill)
    {
        yield return skill.Canonical;
        foreach (var alias in skill.Aliases)
        {
            yield return alias;
        }
    }

    private static bool ContainsTerm(string text, string term)
    {
        var pattern = @"\b" + Regex.Escape(term).Replace(@"\ ", @"[\s\-]?") + @"\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static SkillDefinition Skill(string canonical, params string[] aliases) => new(canonical, aliases);
}
