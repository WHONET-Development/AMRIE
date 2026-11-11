# SQL Reference Queries for Resource Tables

While `AMRIE.Library` executes all joins, taxonomic lookups, and precedence sorting directly in C# to remain cross-platform and database-independent, the resource tables in `AMRIE.Library/Resources/` can be imported directly into any relational database (such as SQLite, PostgreSQL, or SQL Server).

This document provides the canonical SQLite queries (also located in `AMRIE.Library/Resources/*.sql`) that demonstrate how to join `Organisms`, `Breakpoints`, `Antibiotics`, and `ExpectedResistancePhenotypes` while respecting taxonomic specificity.

---

## 1. Querying & Prioritizing Applicable Breakpoints (`Breakpoints.sql`)

This query returns all matching breakpoints for a specific organism (`WHONET_ORG_CODE = 'spn'`), year (`2021`), and antibiotic test (`CRO_NM`), ordered from most specific to least specific (`SEROVAR_GROUP` $\to$ `ANAEROBE`) and by breakpoint type (`Human` $\to$ `Animal` $\to$ `ECOFF`).

```sql
-- Find the breakpoints for the organism specified in the WHERE clause.
-- Breakpoints must be prioritized by the ORDER BY in the order below, which sorts the most specific breakpoints first.
-- For example, the breakpoints for spn appear above the STR genus level breakpoints because they are more specific.
SELECT o.ORGANISM, o.WHONET_ORG_CODE, b.ORGANISM_CODE, b.ORGANISM_CODE_TYPE, b.GUIDELINES, b.YEAR,
	b.TEST_METHOD, b.POTENCY, b.BREAKPOINT_TYPE, b.HOST, b.SITE_OF_INFECTION, b.WHONET_TEST,
	b.R, b.I, b.SDD, b.S, b.ECV_ECOFF
FROM Organisms o
	INNER JOIN Breakpoints b
		ON b.ORGANISM_CODE_TYPE <> 'ALL' AND (
			o.SEROVAR_GROUP IS NOT NULL
			AND b.ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
			AND o.SEROVAR_GROUP = b.ORGANISM_CODE
		) OR (
			b.ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
			AND o.WHONET_ORG_CODE = b.ORGANISM_CODE
		) OR (
			o.SPECIES_GROUP IS NOT NULL
			AND b.ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
			AND o.SPECIES_GROUP = b.ORGANISM_CODE
		) OR (
			o.GENUS_CODE IS NOT NULL
			AND b.ORGANISM_CODE_TYPE = 'GENUS_CODE'
			AND o.GENUS_CODE = b.ORGANISM_CODE
		) OR (
			o.GENUS_GROUP IS NOT NULL
			AND b.ORGANISM_CODE_TYPE = 'GENUS_GROUP'
			AND o.GENUS_GROUP = b.ORGANISM_CODE
		) OR (
			o.FAMILY_CODE IS NOT NULL
			AND b.ORGANISM_CODE_TYPE = 'FAMILY_CODE'
			AND o.FAMILY_CODE = b.ORGANISM_CODE
		) OR (
			o.ANAEROBE = 'X'
			AND b.ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
			AND ((
					o.SUBKINGDOM_CODE = '+'
					AND b.ORGANISM_CODE = 'AN+'
				) OR (
					o.SUBKINGDOM_CODE = '-'
					AND b.ORGANISM_CODE = 'AN-'
			))
		) OR (
			o.ANAEROBE = 'X'
			AND b.ORGANISM_CODE_TYPE = 'ANAEROBE'
			AND b.ORGANISM_CODE = 'ANA'
		)
WHERE o.WHONET_ORG_CODE = 'spn'
	AND o.TAXONOMIC_STATUS = 'C'
	AND b.YEAR = 2021
	AND b.WHONET_TEST = 'CRO_NM'
ORDER BY o.WHONET_ORG_CODE ASC,
	b.GUIDELINES ASC,
	b.YEAR ASC,
	b.TEST_METHOD ASC,
	(
		CASE b.BREAKPOINT_TYPE
		WHEN 'Human' THEN 1
		WHEN 'Animal' THEN 2
		WHEN 'ECOFF' THEN 3
		END
	) ASC,
	b.HOST ASC,
	(
		CASE b.ORGANISM_CODE_TYPE
		WHEN 'SEROVAR_GROUP' THEN 1
		WHEN 'WHONET_ORG_CODE' THEN 2
		WHEN 'SPECIES_GROUP' THEN 3
		WHEN 'GENUS_CODE' THEN 4
		WHEN 'GENUS_GROUP' THEN 5
		WHEN 'FAMILY_CODE' THEN 6
		WHEN 'ANAEROBE+SUBKINGDOM_CODE' THEN 7
		WHEN 'ANAEROBE' THEN 8
		ELSE 9
		END
	) ASC,
	b.WHONET_TEST ASC,
	b.SITE_OF_INFECTION ASC;
```

---

## 2. Querying Expected Resistance Phenotypes / Intrinsic Resistance (`ExpectedResistancePhenotypes.sql`)

This query resolves applicable intrinsic resistance rules for an organism (`WHONET_ORG_CODE = 'efa'`), joining `Organisms`, `ExpectedResistancePhenotypes`, and `Antibiotics` (via either `WHONET_ABX_CODE` or `ATC_CODE` prefix) while excluding organism exceptions (`EXCEPTION_ORGANISM_CODE`) and antibiotic exceptions (`ANTIBIOTIC_EXCEPTIONS`).

```sql
SELECT DISTINCT i.*
FROM Organisms o
	INNER JOIN (
		SELECT *,
			substr(EXCEPTION_ORGANISM_CODE, 1, instr(EXCEPTION_ORGANISM_CODE, ',') - 1) AS [FirstException],
			substr(EXCEPTION_ORGANISM_CODE, instr(EXCEPTION_ORGANISM_CODE, ',') + 1) AS [SecondException]
		FROM ExpectedResistancePhenotypes
	) i
		ON (
			o.SEROVAR_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
			AND o.SEROVAR_GROUP = i.ORGANISM_CODE
		) OR (
			i.ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
			AND o.WHONET_ORG_CODE = i.ORGANISM_CODE
		) OR (
			o.SPECIES_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
			AND o.SPECIES_GROUP = i.ORGANISM_CODE
		) OR (
			o.GENUS_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'GENUS_CODE'
			AND o.GENUS_CODE = i.ORGANISM_CODE
		) OR (
			o.GENUS_GROUP IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'GENUS_GROUP'
			AND o.GENUS_GROUP = i.ORGANISM_CODE
		) OR (
			o.FAMILY_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'FAMILY_CODE'
			AND o.FAMILY_CODE = i.ORGANISM_CODE
		) OR (
			o.SUBKINGDOM_CODE IS NOT NULL
			AND i.ORGANISM_CODE_TYPE = 'SUBKINGDOM_CODE'
			AND o.SUBKINGDOM_CODE = i.ORGANISM_CODE
		) OR (
			o.ANAEROBE = 'X'
			AND i.ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
			AND ((
					o.SUBKINGDOM_CODE = '+'
					AND i.ORGANISM_CODE = 'AN+'
				) OR (
					o.SUBKINGDOM_CODE = '-'
					AND i.ORGANISM_CODE = 'AN-'
			))
		) OR (
			o.ANAEROBE = 'X'
			AND i.ORGANISM_CODE_TYPE = 'ANAEROBE'
			AND i.ORGANISM_CODE = 'ANA'
		)
	INNER JOIN Antibiotics a
		ON (
				i.ABX_CODE_TYPE = 'ATC_CODE'
				AND substr(a.ATC_CODE, 1, length(i.ABX_CODE)) = i.ABX_CODE
		) OR (
				i.ABX_CODE_TYPE = 'WHONET_ABX_CODE'
				AND i.ABX_CODE = a.WHONET_ABX_CODE
		)
WHERE o.WHONET_ORG_CODE = 'efa'
	AND o.TAXONOMIC_STATUS = 'C'
	AND (
			coalesce(i.EXCEPTION_ORGANISM_CODE, '') = ''
			OR NOT (
				(
					o.SEROVAR_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
					AND (o.SEROVAR_GROUP = i.FirstException OR o.SEROVAR_GROUP = i.SecondException)
				) OR (
					i.EXCEPTION_ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
					AND (o.WHONET_ORG_CODE = i.FirstException OR o.WHONET_ORG_CODE = i.SecondException)
				) OR (
					o.SPECIES_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
					AND (o.SPECIES_GROUP = i.FirstException OR o.SPECIES_GROUP = i.SecondException)
				) OR (
					o.GENUS_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'GENUS_CODE'
					AND (o.GENUS_CODE = i.FirstException OR o.GENUS_CODE = i.SecondException)
				) OR (
					o.GENUS_GROUP IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'GENUS_GROUP'
					AND (o.GENUS_GROUP = i.FirstException OR o.GENUS_GROUP = i.SecondException)
				) OR (
					o.FAMILY_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'FAMILY_CODE'
					AND (o.FAMILY_CODE = i.FirstException OR o.FAMILY_CODE = i.SecondException)
				) OR (
					o.SUBKINGDOM_CODE IS NOT NULL
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'SUBKINGDOM_CODE'
					AND (o.SUBKINGDOM_CODE = i.FirstException OR o.SUBKINGDOM_CODE = i.SecondException)
				) OR (
					o.ANAEROBE = 'X'
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
					AND ((
							o.SUBKINGDOM_CODE = '+'
							AND (i.FirstException = 'AN+' OR i.SecondException = 'AN+')
						) OR (
							o.SUBKINGDOM_CODE = '-'
							AND (i.FirstException = 'AN-' OR i.SecondException = 'AN-')
					))
				) OR (
					o.ANAEROBE = 'X'
					AND i.EXCEPTION_ORGANISM_CODE_TYPE = 'ANAEROBE'
					AND (i.FirstException = 'ANA' OR i.SecondException = 'ANA')
				)
			)
		)
	AND (
		coalesce(i.ANTIBIOTIC_EXCEPTIONS, '') = ''
		OR instr(i.ANTIBIOTIC_EXCEPTIONS, a.WHONET_ABX_CODE) = 0
	)
ORDER BY i.GUIDELINE ASC,
	(
		CASE i.ORGANISM_CODE_TYPE
		WHEN 'SEROVAR_GROUP' THEN 1
		WHEN 'WHONET_ORG_CODE' THEN 2
		WHEN 'SPECIES_GROUP' THEN 3
		WHEN 'GENUS_CODE' THEN 4
		WHEN 'GENUS_GROUP' THEN 5
		WHEN 'FAMILY_CODE' THEN 6
		WHEN 'SUBKINGDOM_CODE' THEN 7
		WHEN 'ANAEROBE+SUBKINGDOM_CODE' THEN 8
		WHEN 'ANAEROBE' THEN 9
		ELSE 10
		END
	) ASC,
	(
		CASE i.ABX_CODE_TYPE
		WHEN 'WHONET_ABX_CODE' THEN 1
		WHEN 'ATC_CODE' THEN 2
		ELSE 3
		END
	) ASC,
	i.ABX_CODE ASC,
	a.WHONET_ABX_CODE ASC;
```

---

## 3. Windowed Most-Specific Breakpoint Partitioning (`FilterBreakpointsTable.sql`)

Uses `row_number() OVER (PARTITION BY ...)` to select only the single highest-specificity breakpoint (`RowRank = 1`) per organism, guideline, year, breakpoint type, host, site of infection, and test.

```sql
SELECT DISTINCT GUIDELINES, YEAR, TEST_METHOD, POTENCY, ORGANISM_CODE, ORGANISM_CODE_TYPE, BREAKPOINT_TYPE, [HOST],
	SITE_OF_INFECTION, REFERENCE_TABLE, REFERENCE_SEQUENCE, WHONET_ABX_CODE, WHONET_TEST, R, I, SDD, S, ECV_ECOFF,
	ECV_ECOFF_TENTATIVE, DATE_ENTERED, DATE_MODIFIED, COMMENTS
FROM (
	SELECT row_number() OVER (
		PARTITION BY
			b.GUIDELINES, b.YEAR, b.BREAKPOINT_TYPE, b.[HOST], o.WHONET_ORG_CODE, b.SITE_OF_INFECTION, b.WHONET_TEST
		ORDER BY (
			CASE b.ORGANISM_CODE_TYPE
			WHEN 'SEROVAR_GROUP' THEN 1
			WHEN 'WHONET_ORG_CODE' THEN 2
			WHEN 'SPECIES_GROUP' THEN 3
			WHEN 'GENUS_CODE' THEN 4
			WHEN 'GENUS_GROUP' THEN 5
			WHEN 'FAMILY_CODE' THEN 6
			WHEN 'ANAEROBE+SUBKINGDOM_CODE' THEN 7
			WHEN 'ANAEROBE' THEN 8
			ELSE 9
			END
		) ASC) AS RowRank,
		o.WHONET_ORG_CODE, o.GENUS_CODE, o.SEROVAR_GROUP, b.*
	FROM Organisms o
		INNER JOIN Breakpoints b
			ON b.ORGANISM_CODE_TYPE <> 'ALL' AND (
				o.SEROVAR_GROUP IS NOT NULL
				AND b.ORGANISM_CODE_TYPE = 'SEROVAR_GROUP'
				AND o.SEROVAR_GROUP = b.ORGANISM_CODE
			) OR (
				b.ORGANISM_CODE_TYPE = 'WHONET_ORG_CODE'
				AND o.WHONET_ORG_CODE = b.ORGANISM_CODE
			) OR (
				o.SPECIES_GROUP IS NOT NULL
				AND b.ORGANISM_CODE_TYPE = 'SPECIES_GROUP'
				AND o.SPECIES_GROUP = b.ORGANISM_CODE
			) OR (
				o.GENUS_CODE IS NOT NULL
				AND b.ORGANISM_CODE_TYPE = 'GENUS_CODE'
				AND o.GENUS_CODE = b.ORGANISM_CODE
			) OR (
				o.GENUS_GROUP IS NOT NULL
				AND b.ORGANISM_CODE_TYPE = 'GENUS_GROUP'
				AND o.GENUS_GROUP = b.ORGANISM_CODE
			) OR (
				o.FAMILY_CODE IS NOT NULL
				AND b.ORGANISM_CODE_TYPE = 'FAMILY_CODE'
				AND o.FAMILY_CODE = b.ORGANISM_CODE
			) OR (
				o.ANAEROBE = 'X'
				AND b.ORGANISM_CODE_TYPE = 'ANAEROBE+SUBKINGDOM_CODE'
				AND ((o.SUBKINGDOM_CODE = '+' AND b.ORGANISM_CODE = 'AN+')
					OR (o.SUBKINGDOM_CODE = '-' AND b.ORGANISM_CODE = 'AN-'))
			) OR (
				o.ANAEROBE = 'X'
				AND b.ORGANISM_CODE_TYPE = 'ANAEROBE'
				AND b.ORGANISM_CODE = 'ANA'
			)
	WHERE b.GUIDELINES IN ('CLSI', 'EUCAST')
		AND b.YEAR IN ('2025', '2026')
) nested
WHERE nested.RowRank = 1;
```

