RESEARCH ARTICLE

# Utilizing large language models to construct a
dataset of Württemberg’s 19th-century fauna
from historical records

1

2

Maximilian C. Teich *, Belen Escobari , Malte Rehbein

1

1 Chair of Computational Humanities, University of Passau, Passau, Germany, 2 Center for Biodiversity
Informatics and Collection Data Integration (ZBS), Botanic Garden and Botanical Museum Berlin, Freie
Universität Berlin, Berlin, Germany

* maximilian.teich@uni-passau.de

OPEN ACCESS
Citation: Teich MC, Escobari B, Rehbein M
(2026) Utilizing large language models to
construct a dataset of Württemberg’s 19th-
century fauna from historical records. PLoS
One 21(3): e0344181. https://doi.org/10.1371/
journal.pone.0344181
Editor: Seyyed Kamal Asadi Ojaei, University of
Mazandaran, IRAN, ISLAMIC REPUBLIC OF
Received: October 8, 2025
Accepted: February 17, 2026
Published: March 24, 2026
Copyright: © 2026 Teich et al. This is an open
access article distributed under the terms of
the Creative Commons Attribution License,
which permits unrestricted use, distribution,
and reproduction in any medium, provided the
original author and source are credited.

Data availability statement: The full dataset
is accessible at Zenodo under https://zenodo.
org/records/17277242, as referenced in the
manuscript.

Funding: The author(s) received no specific
funding for this work.

## Abstract

Constructing datasets on past biodiversity from historical sources is crucial for under-
standing long-term ecological changes. Typically, compiling such datasets relies
on prior knowledge of the sources’ composition and requires considerable manual
effort. To overcome these challenges, we implement an automated approach based
on prompted large language models (LLMs) to detect mentions of species in texts
from 19th-century Württemberg and link these mentions to identifiers in the GBIF
database. Based on our evaluation, we find that LLMs can reliably identify species in
the texts with high recall (92.6%) and precision (95.3%), while providing estimates of
the correct species identifier with considerable accuracy (83.0%). As our approach is
easily scalable and adaptable to other contexts and languages, it offers a promising
way to advance dataset generation from historical material using limited resources.

Introduction
Ecosystems and the changes they undergo can only be comprehensively understood
and assessed in light of their past. Estimates of human influence on ecosystems,
explanations for the extinction of certain species, or the formulation of realistic goals
for conservation efforts all rely on knowledge of past developments. Historical ecol-
ogy [1–7] has established itself as a “framework for studying the past and future of
the human–environment relationship” [8]. A central problem of historical ecology—as
in all historical study—is the impossibility of directly observing the past. Instead of
such observation, one must rely on the often incomplete historical record to draw
conclusions about past circumstances. This is especially challenging as many avail-
able data sources are scarce or cover only small time frames or geographic areas.
As Vellend et al. [9] note, this often necessitates the employment of unconventional
methods and a creative use of data sources which may not originally have been
intended to store information on past ecosystems.

Competing interests: The authors have
declared that no competing interests exist.

Reconstructing datasets on past flora and fauna from fragmentary sources is a
central part of historical ecology, ensuring that conserved information pertinent to
present questions can actually be accessed and used by researchers, rather than
being lost to history. The addition of new digital methods promises to help with this
problem. Recently, Rehbein [10] has described these efforts as part of Computational
Historical Ecology, in which “Natural Language Processing, Machine Learning, and
Geospatial Analysis would support the extraction, classification, and integration of
ecological information from a wide variety of archival materials.” The development
and evaluation of approaches integrating such methods is ongoing.

Previous research has addressed the problem of constructing fauna datasets from
textual historical sources in various ways: Some studies extract the relevant infor-
mation largely by hand, as Turvey et al. [11] did for Gaboons from Chinese gazet-
teers, Clavero [12] for fish species in 16th-century Spain, or Govaerts [13] for birds
in 14th-century Holland from financial records. Working with structured data in form
of tables, Rehbein et al. [14] designed a digitization workflow to build a dataset of
44 species documented in an 1845 survey of Bavarian wildlife. Blanco-Garrido et al.
[15] mined geographical dictionaries for species names to derive a dataset of fresh-
water fish in 19th-century Spain, as did Viana et al. [16] for multiple Iberian species.
Clavero et al. [17] combined text queries with spatial modeling to estimate historical
Spanish wolf populations. All these approaches relied either on highly structured
source data or substantial manual effort in compiling the final datasets or in providing
training data for more automated solutions.

In this study, the fauna of the southern German kingdom of Württemberg during
the 19th century is examined. Information on animal sightings from the time was col-
lected in a series of government-issued textual descriptions of the kingdom’s districts.
Presented in prose form, each of these descriptions accounts for regional fauna.
Overall, all descriptions form the material of this study. To generate a dataset of all
animals described in this material and to assess its quality, solutions based on large
language models (LLMs) were implemented and evaluated for the following tasks:
First, all mentions of animals in the texts were recognized, regardless of textual
structure, language (usually Latin or German), or spelling variations, a task generally
known as Named Entity Recognition (NER). Second, these identified tokens (text
units) were used to unambiguously link the documented animal species to a scientific
authority file. To this end, we apply a novel workflow that combines string matching
with LLM-based estimations of the meaning of historical taxa.

We demonstrate that datasets on past fauna can be constructed in a semi-
automated fashion from unstructured sources using LLMs, without fine-tuning and
thus without the need for dedicated training data. In doing so, we overcome the cur-
rent need for manual effort or structured source texts in dataset creation. Rather than
selecting appropriate source texts, reading them, and annotating them manually, we
limit human intervention to providing these sources in machine-readable form, prompt
design, and output evaluation. To evaluate our approach, we use a test dataset that
was manually created by a human expert. We show that species detection from his-
torical texts can be achieved with high recall and precision, despite challenges posed

by historical language. By comparison, linking detected species to authority files is more error-prone but still provides
useful indications of a species and its taxonomic classification.

In conclusion, we consider this approach a valuable option for building datasets from historical textual sources, partic-
ularly when the scope of the material prohibits manual processing. Our approach offers a scalable and adaptable way to
expand the data available in historical ecology.

Materials and methods
Source Material
This study utilizes texts taken from 19th-century regional studies on the 64 Oberämter (administrative districts) of the
southern German kingdom of Württemberg (see Figure 1). This kingdom had been formed only in 1806 under French
hegemony and maintained its status after the political re-organization of 1815. Territorial changes during the Napoleonic
era and the unavailability of reliable land registers motivated the government to begin a systematic land survey of the
kingdom in 1818 [18]. By 1820, a Royal Statistical Office (Königl. Statistisch-Topographisches Bureau) was established to
support this survey. In particular, the office was instructed to record the geography, history, culture, and natural history of
the various districts. This establishment was originally driven by the hopes that a better understanding of the newly inte-
grated districts might help the royal administration increase tax revenues; thus the office was placed under the auspices of

Fig 1. Oberämter. Map of the Oberämter (districts) of the Kingdom of Württemberg in 1848.

https://doi.org/10.1371/journal.pone.0344181.g001

the minister of finance [19]. However, this purpose was soon eclipsed by that of national integration. Since the population
of the new kingdom had a variety of cultural and political traditions that were not necessarily compatible with the idea of
a unified monarchy, the office and its publications soon were instrumentalized to construct a common Württembergian
national identity and to promote Württembergian patriotism [19].

The geographer and statistician Johann Memminger, the office’s first leading figure [20], advocated regional studies
as a means for national integration [21]. In the preface to his 1822 yearbook on Württembergian statistics, he stressed
that “there is no Württembergian people yet; every part is a stranger to the others”. He hoped for a “vitalization of national
spirit” through knowledge of the fatherland (In German: “aber noch haben wir kein würtembergisches Volk; jeder Theil ist
dem andern fremd [...] In demselben Grade aber, in dem die nähere Kenntnis des Vaterlandes und der Mitbürger auf den
Gemeinsinn wirkt, in demselben wirkt sie auch auf Belebung des Volksgeistes” [22]. Hence, the office’s work should not
be seen as merely academic. In light of its political motivation, its projects also appear as attempts to educate the king-
dom’s population on the surrounding regions and to instill a sense of unity.

Between 1824 and 1886, the Royal Statistical Office published 64 volumes of so-called Oberamtsbeschreibungen,
each dedicated to the description of one of the kingdom’s districts and typically released at a pace of about one per year.
Over several decades, these volumes grew more ambitious in scope; whereas the first volume had 158 pages, the last
one was published in two parts with a total of 883 pages. Initially, Memminger compiled the first volumes based on his
own research and information gathered from local contacts. The last volume in the series recounts these early efforts
by Memminger and his assistant and the gradual expansion of the project [23]. As their production became increasingly
professionalized, the editors could rely on a network of civil servants and part-time contributors with higher education
[24]. Unfortunately, the texts generally do not include citations that identify the specific sources used by the editors.
However, the original materials used to compile the texts are still preserved in the archives of the Statistisches Lande-
samt Baden-Württemberg and could serve as a basis for future research into the volumes creation [25]. At least some
of these records had been digitized by 2022 (description of the collection available online at the Statistisches Landes-
amt Baden-Württemberg.) The volumes themselves are available as scans through various public libraries and Google
Books. A Wikisource project has gathered these publicly available sources and manually transcribed all volumes into
machine-readable texts (Wikisource.) These data are used in this study.

A notable feature of the Oberamtsbeschreibungen is the inclusion of a chapter on the fauna of each respective district,
providing valuable historical insights into regional biodiversity. These chapters serve as the foundation for this study, offer-
ing a wealth of textual information. However, the quality of the fauna chapters varies greatly between the different vol-
umes. While some merely note the absence of special or remarkable animals (e.g., in Kirchheim 1842, Künzelsau 1883,
Crailsheim 1884), others provide detailed accounts listing hundreds of species. In some cases, the authors also discuss
the significance of specific animals for local economies or cultural traditions. For example, the volume on Neckarsulm
(1881) relates details on species of fish caught in the district, their culinary appeal to the local population, and special
techniques for catching barbels (Barbus fluviatilis) with tridents in wintertime. The lengths of the chapters range from a
brief 18 words for the rural Künzelsau district to a comprehensive 9682 words in the well-studied university town of Tübin-
gen (1867), with a mean chapter length of 885 tokens and a general trend toward longer texts in later publication years. In
total, by extracting the fauna chapters from each volume, a data set of 56640 tokens was created.

Entity Recognition
The first task is the recognition of n-grams naming animal taxa as presented in the chapters. This task also includes col-
lecting information on the presence of a given animal, as some species are often recorded by the authors as being absent
or extinct.

Previous research has looked into similar entity recognition problems in various ways. In cases where the texts have
a predictable structure, such as in tables, encyclopedic entries, or uniform paragraphs, entities can often be identified

by searching for specific patterns or strings [26–29]. For texts without such structure, some authors have applied
machine learning [30–32]. Several datasets have been published to help evaluate such approaches, but are not tailored
to specific historical settings [33–38]. In addition to traditional machine learning, fine-tuned language models have been
integrated into workflows for species recognition [39–41]. Ehrmann et al. [42] offer an overview of all these approaches
and outline the challenges commonly encountered when dealing with named entity recognition (NER) in historical
documents. More recently, research has focused on the use of prompt-based LLMs for retrieving named entities from
historical texts without additional training data. While Tudor et al. [43] highlights the difficulties posed by hallucinations
and Gonzales [44] finds this approach less effective than fine-tuned neural models, Hiltmann et al. [45] shows that it
can substantially outperform traditional NER frameworks that lack training data, and emphasizes its potential benefits
for historical research. In the realm of biology, Gougherty and Clipp [46] demonstrate how LLMs can be used to reliably
extract information on the spread of pathogens from the literature, as does Scheepens et al. [47] for invertebrate pests
and pest control agents. In a previous study, we looked at identifying common problems in the detection of species
names in historical texts by using a small sample of the Oberamtsbeschreibungen, without attempting to process the
entire corpus [48].

The following challenges to solving the entity recognition task were observed when assessing the historical texts:

• First, the texts share no common structure. Some volumes tend to enumerate species names in long sentences,
whereas others devote multiple sentences to details on a species. Hence, one has to account for all possible ways
in which animals may be referenced. This can include names for individual species or names for an entire class of
animals.

• Second, animals can appear under their German vernacular names, scientific names, or both, which is not consis-
tent across volumes. For example, the fire salamander (Salamandra maculosa) is noted in Backnang (1832) as “der
gefleckte Erdmolch (Salamandra maculosa),” in Stuttgart (1851) just as “Salamandra maculosa an feuchten Orten,” in
Oberndorf (1868) as “der Erdmolch [ist] nicht selten,” and in Spaichingen (1876) as “der gefleckte Salamander (Sala-
mandra maculosa).” Therefore, any recognition method should be capable of determining when two different names
refer to the same animal.

• Third, the names themselves often deviate from modern spellings. For example, the kestrel Falco tinnunculus is
usually given as “Thurmfalke” instead of the modern “Turmfalke”, the burying beetle Nicrophorus vespillo as “Tod-
tengräber” instead of “Totengräber.” Some historical vernacular and scientific names are obscure or no longer in
common use. Scientific names are also frequently abbreviated. These problems may render them unintelligible even
to domain experts.

Considering these multiple challenges and the general absence of task-specific training data, a zero-shot approach
using large language models (LLMs) could be a viable solution.

To allow LLM-based recognition, the historical texts were first split into chunks (text spans) of 200 tokens, which proved
to be a manageable size in the subsequent process. An overlap of 50 tokens between the chunks ensures that each
animal name is included in full in at least one chunk. This process resulted in a total of 934 chunks. Each chunk was then
submitted to an LLM following a prompt specifying the task: first to list all animals mentioned in the historical text by both
their vernacular and scientific names, if provided, and second to indicate whether each animal is described as present or
absent in the district by the authors. The second part of the task does not indicate an actual observation of an animal, but
instead assesses whether the authors regarded a species as occurring or absent in a district. The prompt also included
instructions to return the results as a JSON element including the vernacular name, scientific name, and presence infor-
mation, for example about the European goldfinch (Carduelis carduelis):

{“german_name”: “Distelfink,” “scientific_name”: null, “present”: true}

This format requirement was enforced through a validation loop, which repeated the prompt until a correctly structured
response was received.

As a means of evaluating the quality of responses generated by different LLM and prompt variations, a test dataset
was created. For 50 randomly sampled text chunks, we annotated all names and information about the animals’ presence
using the same JSON format. We used the same spelling as the original historical texts. When evaluating the authors’
assessment of a species’ occurrence in the district, we considered an animal to be occurring when no absence was
implied. This annotation yielded data for 435 mentions of animals in the sample. Using these test data as ground truth, the
LLM responses were evaluated using three metrics:

1. A recall score showing how many of the animals found by the human annotator were also recognized by the LLM.

2. A precision score, indicating how many of the animals found by the LLM were correct, i.e., also annotated by the
human.

3. An additional precision score for the information on presence, showing in how many cases the LLM agreed with the
human annotator on whether an animal is described as being present or absent.

This proved challenging, as all tested LLMs tended to modernize historical spellings in their outputs. Additionally,
the texts often used abbreviated scientific names, which the LLMs attempted to fully spell out. To address these
issues, we applied two different matching strategies to compute the metrics, following the approach of Gonzales-
Gallardo et al. [44]:

1. A strict matching function, which only considers items a match if the vernacular or scientific names are exact string
matches.

2. A fuzzy matching function, which allows for minor variations (an editing distance of 2 was set as acceptable) and
assumes that a match is still valid if the first part of a scientific name is abbreviated in either the human’s or the LLM’s
output.

We tested three common LLMs by adjusting the instruction for optimal performance: Various ways to formulate
the task as prompts were iteratively tested by evaluating the respective outputs against the test data and adjusting
the German wording used for optimal performance. Across the tested prompts, the evaluation results differed only
marginally. The best-performing prompt ultimately used for the task is shown in S3 Prompts. We found that the
models can generally detect the majority of documented species in the texts. GPT-4o performed best on the task,
followed by the Gemma 2 (see Table 1). As one would expect, the metrics’ values are higher when fuzzy matches
are considered, giving the best model a recall score of 93% and a precision score of 95%. Under strict matching
conditions, the recall and precision scores drop to 83% and 84%, respectively. Regarding the presence or absence
of animals, all models score very high. As the calculation of this metric can only consider the matches found
between the Ground Truth and the solutions of the LLMs, there is no discernible difference between the two match-
ing functions. Generally, the best performance of an LLM is comparable to that reported for tools tuned on training
data to identify taxa [36,38].

Table 1. Evaluation metrics for tested LLMs on entity recognition task. Edit distance for fuzzy matching is set at 2.

Model
Gemma 2
GPT-4o
Llama 3.1

Strict Recall
80.2%
82.8%
76.8%

Strict Precision
82.3%
84.2%
82.3%

Strict Presence
97.4%
98.1%
94.0%

Fuzzy Recall
91.0%
92.6%
87.8%

Fuzzy Precision
93.4%
95.3%
93.5%

https://doi.org/10.1371/journal.pone.0344181.t001

Fuzzy Presence
97.5%
98.0%
94.8%

Data Linking
Building on the extraction of vernacular and scientific names from the texts, a second task involves linking these names to
records in an authority file. By mapping the names to such unique records, historical mentions of animals can be dis-
ambiguated, and additional information on each taxon can easily be added for further analysis. The Global Biodiversity
Information Facility (GBIF), an international network and data infrastructure project which aggregates data on taxa from a
wide range of sources, was chosen as the target authority file. GBIF assigns a unique identifier to each taxon and records
all names associated with it along with other information such as distribution data. For each mention of an animal in the
historical texts—whether it is given by a scientific name, a German vernacular name, or both—the corresponding identifier
in GBIF must be found.

Two approaches were considered to complete the task. The first involved matching tokens identified in the historical
text against the GBIF database using its API (GBIF API Reference.) This API allows for the search of scientific names,
supports fuzzy searches, and returns confidence scores for uncertain matches. The separate API function for search-
ing vernacular names is problematic, as this is implemented as a simple text search. This function can return irrelevant
results from the database’s resources. For example, a search for the German vernacular name “Bär” (Ursus arctos) may
also return other species described in the literature by the botanist with the same surname: Johannes Bär. To avoid this
ambiguity, the API was bypassed by downloading parts of the GBIF database and performing local queries. As a basis,
a dataset containing approximately 12,500 German vernacular names (compiled by Holstein and Monje [49]) was used.
This dataset includes names of animals and plants from southern Germany and is itself integrated into GBIF. The data
were further enriched by retrieving all synonymous vernacular names listed in GBIF for each record. Historical vernacular
names were then matched against this dataset to identify the corresponding GBIF entries. However, the direct lookup of
names is still prone to several issues: historical names often exhibit spellings diverging from modern conventions, and sci-
entific names are frequently abbreviated. Additionally, some historical names may have fallen out of use or are too rare to
have found entry into GBIF. In such cases, one cannot expect to find corresponding matches in the dataset. There is also
the possibility of taxa having acquired different meanings or having been split up over time, so even matches returned
by this approach could lead to factually incorrect results. Given these problems, the results of the direct lookup approach
should be expected to generally have a low recall at a high—but not perfect—precision.

The second approach to matching historical names and authority records employs LLMs. Previous research has
explored the use of prompt-instructed LLMs for entity matching tasks. Farrell et al. [50] suggest using such models to
harmonize data directly from textual sources. Peeters et al. [51] find them to be effective in applications where no train-
ing data are available or where many unseen cases are expected, as is the case in our task. However, the quality of the
results depends on the extent of knowledge represented within the models. Dorm et al. [52] and Elliott and Fortes [53]
tested the knowledge of popular LLMs regarding geographical species distribution, showing varying results which likewise
suggest a strong dependence on the models’ training data. Castro et al. [54] used LLMs to identify species in research
papers and newspaper articles with a few-shot approach and also found that the models tested vary widely in their ability
to perform the task. As we must assume that common LLMs are not necessarily trained on 19th-century German biologi-
cal texts, we devised prompts in a way to pass both the historical names and the original text chunk to an LLM, to ensure
that there is some context available. Based on these inputs, the model is instructed to suggest a modern scientific name
for the animal in question. Initial experimentation showed that this LLM-based approach yielded less precise results than
the pure database lookup approach, even with the contextual information offered by the text chunks. At the same time, it
achieved perfect recall, as the LLM would always return at least a plausible result.

Given the shortcomings of both approaches, we designed a combined workflow for optimized performance on the task:
In a first step, the extracted scientific and vernacular names are looked up in the locally stored copy of the database. Only
cases where no match is found are passed to a second step, in which an LLM is prompted to provide the animal’s modern
scientific name based on the historical names and contextual information. Prompt design again involves iterative testing of

prompts, with the best performing prompt shown in S3 Prompts. The result can then be used to attempt another lookup in
the GBIF database (see Figure 2).

To evaluate the quality of the results produced by this combined approach, an additional test dataset was manually
created as a ground truth. For 300 randomly selected triples consisting of scientific names, German vernacular names,
and the original text chunk as context, initial experimental results were evaluated by a domain expert. The expert also
recorded the correct solution for each case. However, in 27 of the 300 test cases (9%), even the expert could not deter-
mine the correct solution with certainty. For example, for Freudenstadt (1858) authors documented “den Schaumwurm
(Cercopis spumaria).” This scientific name is a synonym of Philaenus spumarius (common meadow spittlebug; Wie-
senschaumzikade in German), but neither the common name, nor the historical synonym is recorded in GBIF or any
other information source. These uncertain cases were excluded from the accuracy calculation but suggest a noteworthy
baseline error for the task. After the evaluation, all synonyms of the correct solutions available were queried from GBIF
to collect all possible valid answers. Against these lists of valid answers, both the GBIF lookup results and the LLM out-
puts could be automatically evaluated. An accuracy score was introduced to indicate how often the combined approach
matched the solution of the domain expert. A recall metric indicates for how many cases a solution was found.

Fig 2. Workflow. Combined approach workflow using direct lookup of names in the GBIF database and an LLM in case historical names are not found
directly.

https://doi.org/10.1371/journal.pone.0344181.g002

Results of the evaluation are shown in Table 2. Using only the direct lookup of historical names in the GBIF database,
the results of the domain expert could be reproduced in 92% of the test cases. However, this approach alone only yields
results in 61% of cases and returns no result in the remaining 39. When an LLM is included to find solutions for these
remaining cases by suggesting modern equivalents to the historical names not found directly in the database, all of them
were solved. However, with the introduction of the LLMs, the overall accuracy drops, as the suggested names are less
likely to conform with the expert’s solution. The best performance was achieved by using Llama 3.1, with an accuracy of
83% with the remaining inaccurate results often close to the true solution. For example, the “Saatgans” (Anser fabalis)
is falsely linked to the “Graugans” Anser anser, while the “Birkhuhn” (Tetrao tetrix) is mistaken for its British subspecies
Tetrao britannicus.

It should be noted that the performance of the task depends on the kind of test case observed. This can be seen when
grouping the cases in the test dataset by the taxonomic class and evaluating each class separately (see Table 3). The
largest group in the dataset are birds, accounting for 139 of the test cases. For this group, Llama 3.1 achieves an accu-
racy of 87%. Amphibians (n = 11) and Mammals (n = 50) show the best results with accuracy scores of 91% and 88%,
respectively, also with Llama 3.1. At the other end of the spectrum, insects (n = 23) can only be linked correctly in 48% of
cases. The differences in task performance on these classes may result from characteristics of the historical texts and the
models used. Taxa for amphibians and insects may have been less likely to become canonized than those for birds and
mammals, making it harder for LLMs trained on modern biological literature to place them in taxonomies. The compara-
tively smaller sample size for these groups in the test dataset may have amplified this effect.

Considering these variations in the performance of the workflow, the evaluation was further extended to additional
taxonomic levels: For each element in the test set, GBIF entries on phylum, class, order, family, and genus were queried.
Non-empty values were then compared to those for the outputs of the various LLMs to evaluate performance on each
level. The results are shown in Table 4. For phylum and class, all LLMs produced nearly perfect results, with errors no
greater than about 2%, even at the order level. However, performance noticeably declined at the family level, dropping to
an accuracy of only 89% for the best-performing Llama 3.1 model at the genus level. These findings suggest that most
of the method’s errors arise from confusion at the lower taxonomic levels—family and genus. Nevertheless, the resulting
information about an animal’s order, class, and phylum is likely to be accurate, even if the method fails to link to the cor-
rect species identifier.

Lastly, we evaluated the performance of the LLMs with respect to the diachronic nature of sources. Considering the 62
years time span in which the documents were written, we hypothesized that animal names became more standardized

Table 2. Evaluation metrics for the combined approach to the data linking task.

Model
Gemma 2
GPT-4o
Llama 3.1
without LLM

Recall
100.0%
100.0%
100.0%
60.9%

https://doi.org/10.1371/journal.pone.0344181.t002

Table 3. Accuracy results by species class.

Model
Gemma 2
GPT-4o
Llama 3.1
without LLM

Amphibia
72.7%
72.7%
90.9%
100.0%

https://doi.org/10.1371/journal.pone.0344181.t003

Aves
79.6%
99.6%
87.1%
94.5%

Accuracy
77.0%
80.7%
83.0%
92.0%

Insecta
43.5%
43.5%
47.8%
62.5%

Mammalia
82.0%
88.0%
88.0%
92.0%

Table 4. Accuracy results for other levels of the taxonomy.

Model
Gemma 2
GPT-4o
Llama 3.1
without LLM

phylum
99.2%
99.6%
99.6%
99.4%

class
99.2%
99.6%
99.6%
99.3%

https://doi.org/10.1371/journal.pone.0344181.t004

order
98.1%
97.7%
98.1%
99.4%

family
93.2%
94.0%
92.5%
98.1%

genus
87.2%
88.2%
88.9%
96.3%

over time and approached modern conventions, which should lead to better model performance for more recent texts.
To account for the uneven distribution of the data over the years, the test dataset was divided evenly into four quarters
of equal token size. The LLMs’ outputs were then evaluated separately for each quarter. Contrary to our expectation, the
results shown in Table 5 do not indicate a trend toward more easily interpretable names over time. While the best-
performing Llama 3.1 model scores highest on the most recent quarter of names from the 1870s and 1880s, it shows its
worst performance on the second most recent quarter. The other two models achieve their best results on the second
quartile, which contains names from the 1850s and 1860s. Overall, the diachronic analysis of the LLMs’ performance
yields no significant results.

Results
A dataset of the animals documented in the Oberamtsbeschreibungen was built using the entity recognition workflow with
GPT-4o and the data linking method with Llama 3.1 (see S1 Biodiversity Dataset). Additionally, the dataset includes infor-
mation created by employing only a database lookup, without using an LLM for linkage. This is intended for applications
that require higher precision, at the expense of significantly lower recall of the animals mentioned in the original texts. In
both cases, duplicates where animals are mentioned multiple times within a district were removed, as these typically result
from overlapping text chunks.

The dataset, which is expected to capture the vast majority of animal mentions from the original texts, includes 6733
entries after removal duplicates. Figure 3 shows how these entries are distributed between districts and publication years.
In three districts, no animal names could be detected. These results are correct insofar as the corresponding texts state
that no research was conducted on the fauna of the district (Kirchheim) or that the authors stated that the fauna does not
differ noticeably from that of a previously described neighboring district (Crailsheim and Künzelsau), thus referring to the
other volumes. For the remaining districts, the number of detected animal mentions ranges from two in Cannstatt to 499
in Tübingen, with a mean of 106 mentioned animals. In general, the number of mentions increases over time. Simulta-
neously, the overall length of the fauna sections normalized to the number of animals documented decreases in the later
volumes, suggesting that the texts became less descriptive over time and began to favor enumerations.

In addition, the distribution of animal classes over time can be assessed within the dataset. Birds are mentioned most
frequently overall, comprising 45.3% of the records, followed by insects (15.9%) and mammals (10.8%). For 11.5% of
the animals found, there is no class entry in GBIF; based on the test dataset, these cases can be assumed to be almost
exclusively fish, which are generally not assigned a class value. Figure 4 illustrates how the class values are distributed

Table 5. Accuracy results for four time spans.

Model
Gemma 2
GPT-4o
Llama 3.1

1825-1852
72.6%
81.4%
82.9%

https://doi.org/10.1371/journal.pone.0344181.t005

1853-1863
80.1%
83.8%
82.3%

1865-1871
78.3%
78.2%
79.7%

1872-1886
76.2%
79.3%
87.3%

Fig 3. Taxa distribution. Distribution of detected taxa over districts and years.

https://doi.org/10.1371/journal.pone.0344181.g003

Fig 4. Class proportions. Proportions of most frequent classes of animals by districts and years. Regression lines in red indicate trends.

https://doi.org/10.1371/journal.pone.0344181.g004

across the corpus. The share of birds remains high throughout and increases in the later texts. Both, the proportions of
mammals and animals without a class value decrease over time, whereas the proportion of insects grows. The relatively
smaller shares of less common classes—such as amphibians, gastropods, and squamates—remain relatively stable.
To gain a better understanding of the spatial distribution of the data, it can be projected onto a map of the districts.

Since there is no readily available vectorized geodata, we created a custom GeoJSON file of the borders of the Oberäm-
ter. This is based on a publicly available mid-19th-century topographical map of Württemberg at a scale of 1:200,000,
produced by the Royal Statistical Office [55]. The map shows the district borders in 1848, during the time the Oberamts-
beschreibungen were edited and published. As the borders were rarely and only slightly adjusted after 1810 [56], we
assume the map gives a good representation of the Oberämter’s extent. This historical map was georeferenced and
retraced using the open-source QGIS software (see S2 Supporting Geodata). Figure 5 visualizes the number of animals
mentioned per district on the resulting map. It clearly shows that the central and southern districts—surveyed earliest—
mention fewer animals. Conversely, the volumes for more remote districts in the west and north, generally published after
1850, contain more mentions. The university town of Tübingen (published 1867) stands out clearly as an outlier, suggest-
ing it had been studied more intensively than any other district in the kingdom.

The same visualization approach can be used to identify where individual species were documented as occurring
or being absent. Even shorter texts usually document mammals that were common game animals. The latter are often
described in the greatest detail and tend to be mentioned even when extinct in a district, sometimes with information on
when they were last spotted or shot. For example, Figure 6 shows mentions of wild boar (Sus scrofa), roe deer (Capreo-
lus capreolus), and hare (Lepus europaeus). The wild boar is frequently documented in early volumes, albeit only in the
context of its extinction in the course of the 19th century. The later volumes largely omit references to wild boars, perhaps
because their absence had come to be expected. Only four cases indicate the occurrence of wild boars and the respec-
tive texts explicitly describe them as rare and migrating from beyond the kingdom’s borders. In Mergentheim, the text even
recounts in detail two boars being shot in 1870 and another spotted by a hunting party of military officers, highlighting
how remarkable these occurrences were. In contrast, roe deer and hares are generally assumed to be present. Devia-
tions from this expectation are often explained in the texts, such as for Maulbronn (1870), where the authors note that
“the number of hunting enthusiasts is increasing by the day” and this had—along with illness—decimated the local hare
population (in German: “Der Hase ist in Folge der sich täglich mehrenden Jagdfreunde, sowie verschiedener Krankheiten,
namentlich Leberleiden, in neuerer Zeit seltener geworden [...]”).

Discussion
The problem of generating a fauna dataset for the Kingdom of Württemberg from unstructured 19th-century source texts
in an automated manner was addressed by breaking it down into two tasks: The first task involved detecting animals men-
tioned in the texts as named entities. This was challenging due to the lack of structure in the texts, the archaic language,
and the general uncertainty regarding the expected taxa. It was found that processing the texts in chunks with a prompted
LLM and enforcing a structured response led to reasonably good results when evaluated using strict string matching met-
rics (recall = 82.8%, precision = 84.2%). When more lenient, but arguably more appropriate, fuzzy matching criteria were
applied, this yielded better results (recall = 92.6%, precision = 95.3%). This suggests that, while not perfect, an LLM-based
approach can detect biological taxa even in unstructured historical texts.

The second task of linking the retrieved taxa to entities in an authority file proved more difficult. Due to the use of
archaic names in the source texts, finding a correct match was not possible in a substantial number of cases, even for
human experts. The combined approach employed here—first looking up the retrieved names in a database of known
taxa, and then prompting an LLM to suggest a modern equivalent if that failed—worked reasonably well. Overall, this
approach can be expected to yield a species-level accuracy of 83.0% based on the test data. While this still implies a rel-
atively high error rate, it should be noted that the accuracy scores are better at higher taxonomic levels. When identifying

Fig 5. Animals by district. Number of extracted animal mentions by district.

https://doi.org/10.1371/journal.pone.0344181.g005

Fig 6. Animal Occurrences. Occurrence of wild boar, roe deer, and European hare in the districts according to source text. Green colour indicates
presence, red absence, white no data for the district.

https://doi.org/10.1371/journal.pone.0344181.g006

the class or order of an animal, the approach achieved very high accuracy scores of 99.6% and 98.1%, respectively. This
shows that the approach, even though it may not reach human-level quality in linking historical taxa to modern records,
can produce results of predictably high quality.

Given these results, a dataset of known quality has been generated. The dataset itself may be used to expand existing
collections of biodiversity data, such as GBIF. It can also inform subsequent research concerned with Württembergian wildlife
by serving as a baseline. Methodologically, this work demonstrates that prompted LLMs can be valuable for building workflows
to generate biodiversity datasets from historical text sources in a semi-automated fashion. In particular, an LLM-based approach
offers several advantages: it requires no predefined structure of the source texts, no prior knowledge of the animals described,
and no need for special training data or fine-tuning. Additionally, it demands little manual effort: In our case, only the initial prepa-
ration of the source texts, the prompt design, and the creation of the test data set used for evaluation were performed manually.

Furthermore, our approach could be easily adapted to similar sources of a different regional context. Since the tested LLMs
performed reasonably well on historical German texts when solving an expert task without specific training, one can expect that
they might deliver similar results on comparable textual sources in other languages, historical dialects, and regions likely to be
represented in the models’ training data. Working with texts written in less common languages could result in much less reliable
outputs. The same holds true for texts of cultural traditions and contexts in which animal names do not map as well on modern
taxonomies. Whereas the Oberamtsbeschreibungen mostly conform to modern European standards of animal classification and
naming conventions, other historical texts may only allow for an approximate placing of animal mentions in modern taxonomies.
Still, the evaluation procedure allows an estimate of the workflow’s performance when employed with case-specific test data.
Compared to datasets created by human experts from historical documents, such as [13,17], the advantages of an LLM-
based approach are remarkable. Particularly in the analysis of historical source texts, where large amounts of data and a
requirement for niche expertise may render traditional approaches unfeasible, LLMs can offer a viable alternative. However,
these advantages may come at the expense of dataset quality especially at the lower taxonomic levels, as shown by our eval-
uation against a human expert’s benchmark results. When choosing to implement such an approach, it is important to consider
the trade-offs involved. If available resources are limited relative to the scope of the historical sources, the use of LLMs is likely
a promising option. The same holds true in cases where only a general assessment of the data is needed—for example, when
an imperfect recall of mentioned species or a broad analysis of order-level taxonomic distribution is sufficient. Conversely, when
data of nearly perfect quality are required, human oversight remains indispensable. In any case, it is advisable to implement a
method for evaluating the quality of the resulting dataset in order to be able to estimate the extent of potential errors.

In conclusion, we are confident that LLM-driven approaches to semi-automated dataset generation from textual
sources will help mobilize knowledge on past biodiversity at a greater scale in the future. Particularly in cases where
manual processing of large quantities of textual sources is not feasible, such workflows can offer an efficient alternative to
expand the body of data available to historical ecology.

Supporting information
S1 Text. Biodiversity Dataset. CSV file representing all species documented in the source texts, with the name of
the district, the year of publication, the original text, the species names as given in the original text, information on the
presence of the species, a normalized species name, and a GBIF identifier. Available from: https://doi.org/10.5281/
zenodo.17277241
(CSV)
S2 Text. Supporting Geodata. GeoJSON file representing the historical boundaries of the 64 districts (Oberämter) of
the Kingdom of Württemberg circa 1848. The data were derived from a contemporaneous map by Franz von Mittnacht
through manual retracing. Available from: https://doi.org/10.5281/zenodo.18161575
(GEOJSON)

S3 Text. Prompts. Final German prompts used for the entity recognition and data linking tasks in dataset generation.
(TXT)

Author contributions
Conceptualization: Maximilian C. Teich, Malte Rehbein.

Data curation: Maximilian C. Teich, Belen Escobari.

Formal analysis: Maximilian C. Teich.

Investigation: Maximilian C. Teich, Belen Escobari.

Methodology: Maximilian C. Teich, Belen Escobari, Malte Rehbein.

Project administration: Maximilian C. Teich.
Software: Maximilian C. Teich.
Supervision: Malte Rehbein.

Validation: Belen Escobari.

Visualization: Maximilian C. Teich.

Writing – original draft: Maximilian C. Teich.

Writing – review & editing: Malte Rehbein.

### References

1. Crumley CL. Historical ecology: cultural knowledge and changing landscapes. University of New Mexico Press. 1994. Google-Books-ID:

EDwx1eGD1PEC.

2. Balée WL. Advances in Historical Ecology. Columbia University Press. 1998. Google-Books-ID: PuadAwAAQBAJ.
3. Swetnam TW, Allen CD, Betancourt JL. Applied historical ecology: using the past to manage for the future. Ecol Appl. 1999;9(4):1189–206. https://
doi.org/10.1890/1051-0761(1999)009[1189:aheutp]2.0.co;2
4. Sutton MQ, Anderson EN. An Introduction to Cultural Ecology. London: Routledge. 2004. https://doi.org/10.4324/9781003135456
5. Balée W. The research program of historical ecology. Ann Rev Anthropol. 2006;35(10):75–98. https://doi.org/10.1146/annurev.

anthro.35.081705.123231

6. SZABÓ P. Why history matters in ecology: an interdisciplinary perspective. Envir Conserv. 2010;37(4):380–7. https://doi.org/10.1017/
s0376892910000718

7. Isendahl C, Stump D. Introduction: The construction of the present through the reconstruction of the past. In: Isendahl C, Stump D, editors.

The Oxford Handbook of Historical Ecology and Applied Archaeology. Oxford University Press. 2015. p. xvii–xxxiv. https://doi.org/10.1093/oxfor
dhb/9780199672691.002.0007

8. Crumley CL. Historical Ecology. The International Encyclopedia of Anthropology. John Wiley & Sons, Ltd. 2018. p. 1–5. https://doi.

org/10.1002/9781118924396.wbiea1887

9. Vellend M, Brown CD, Kharouba HM, McCune JL, Myers-Smith IH. Historical ecology: using unconventional data sources to test for effects of
global environmental change. Am J Bot. 2013;100(7):1294–305. https://doi.org/10.3732/ajb.1200503 PMID: 23804553
10. Rehbein M. From historical archives to algorithms: reconstructing biodiversity patterns in 19th century Bavaria. Diversity. 2025;17(5):315. https://
doi.org/10.3390/d17050315

11. Turvey ST, Crees JJ, Di Fonzo MMI. Historical data as a baseline for conservation: reconstructing long-term faunal extinction dynamics in Late
Imperial-modern China. Proc Biol Sci. 2015;282(1813):20151299. https://doi.org/10.1098/rspb.2015.1299 PMID: 26246553
12. Clavero M. The King’s aquatic desires: 16th‐century fish and crayfish introductions into Spain. Fish and Fisheries. 2022;23(6):1251–63. https://doi.

org/10.1111/faf.12680

13. Govaerts S. Biodiversity in the Late Middle Ages: Wild Birds in the Fourteenth-Century County of Holland. Environment and History. 2023:1–26.

https://doi.org/10.3828/096734022x16627150608122

14. Rehbein M, Escobari B, Fischer S, Güntsch A, Haas B, Matheisen G, et al. Quantitative and qualitative Data on historical Vertebrate Distributions
in Bavaria 1845. Sci Data. 2025;12(1):525. https://doi.org/10.1038/s41597-025-04846-8 PMID: 40155652

15. Blanco-Garrido F, Hermoso V, Clavero M. Fishing historical sources: a snapshot of 19th-century freshwater fauna in Spain. Rev Fish Biol Fisheries.

2023;33(4):1353–69. https://doi.org/10.1007/s11160-022-09753-4
16. Viana DS, Blanco-Garrido F, Delibes M, Clavero M. A 16th-century biodiversity and crop inventory. Ecology. 2022;103(10):e3783. https://doi.

org/10.1002/ecy.3783 PMID: 35668026

17. Clavero M, García‐Reyes A, Fernández‐Gil A, Revilla E, Fernández N. Where wolves were: setting historical baselines for wolf recovery in Spain.

Animal Conservation. 2022;26(2):239–49. https://doi.org/10.1111/acv.12814
18. Grams G, Heß D, Paul T, Schleyer A, Steudle G. 200 Jahre Landesvermessung und Liegenschaftskataster – ein Bogenschlag vom Königreich
Württemberg zu digital@bw. ZfV - Zeitschrift für Geodäsie, Geoinformation und Landmanagement. 2018;4(zfv 2018). https://doi.org/10.12902/
zfv-0223-2018

19. Stecker S. Der “Schwäbische Volkscharakter” wird konstruiert württembergische Oberamts- und Landesbeschreibungen des 19 Jahrhunderts.

Schwabenbilder. Zur Konstruktion eines Regionalcharakters. Tübingen: Tübinger Vereinigung für Volkskunde e. V. 1997. p. 89–94.

20. Memminger QF, Johann Daniel Georg von. In: Neue Deutsche Biographie. vol. 17. Historische Kommission bei der Bayerischen Akademie der
Wissenschaften; 1994. p. 31–2. Available from: https://www.deutsche-biographie.de/pnd100207359.html#ndbcontent
21. Keller-Drescher L. Das statistisch-topographische Bureau als Transaktionsraum ethnographischen Wissens. In: Berg G, Török BZ, Twellmann M,
editors. Berechnen/Beschreiben. Praktiken statistischen (Nicht-)Wissens 1750-1850. No. 104 in Historische Forschungen (HF). Berlin: Duncker &
Humblot. 2015. p. 79–95.

22. Memminger JDG. Würtembergische Jahrbücher für vaterländische Geschichte, Geographie, Statitik und Topographie. Stuttgart. 1822.
23. Paulus E. Beschreibung des Oberamts Ellwangen. No. 64. Die Beschreibung des Königreichs Württemberg. Stuttgart: Bissinger. 1886.
24. Keller-Drescher L. Landesbeschreibung als Wissensformat - Ansätze zu einer vergleichenden Analyse. In: Johler R, Wolf J, editors. Beschreiben
und Vermessen. Raumwissen in der östlichen Habsburgermonarchie im 18. und 19. Jahrhundert. Berlin: Frank & Timme. 2020. p. 207–26.

25. Burkhardt M. Akten zu den württembergischen Oberamtsbeschreibungen im Staatsarchiv Ludwigsburg erschlossen. Archivnachrichten.

2022;(16):8. https://doi.org/10.53458/an.vi16.4250

26. Smail R, Donaldson C, Govaerts R, Rayson P, Stevens C. Uncovering Environmental Change in the English Lake District: Using Computational
Techniques to Trace the Presence and Documentation of Historical Flora. Digital Scholarship in the Humanities. 2021;36(3):736–56. https://doi.
org/10.1093/llc/fqaa047

27. Paragkamian S, Sarafidou G, Mavraki D, Pavloudi C, Beja J, Eliezer M, et al. Automating the Curation Process of Historical Literature on Marine
Biodiversity Using Text Mining: The DECO Workflow. Front Mar Sci. 2022;9. https://doi.org/10.3389/fmars.2022.940844
28. Coleman D, Gallagher RV, Falster D, Sauquet H, Wenk E. A workflow to create trait databases from collections of textual taxonomic descriptions.

Ecological Informatics. 2023;78:102312. https://doi.org/10.1016/j.ecoinf.2023.102312
29. Liang K, Huang C-R, Jiang X-L. From Text to Historical Ecological Knowledge: The Construction and Application of the Shan Jing Knowledge
Base. In: Proceedings of the 2024 Joint International Conference on Computational Linguistics, Language Resources and Evaluation (LREC-
COLING 2024), Torino, Italia: ELRA and ICCL; 2024. 7521–30. https://doi.org/10.63317/53rov2ncdvak
30. Langer L, Burghardt M, Borgards R, Böhning‐Gaese K, Seppelt R, Wirth C. The rise and fall of biodiversity in literature: A comprehensive quanti-
fication of historical changes in the use of vernacular labels for biological taxa in Western creative literature. People and Nature. 2021;3(5):1093–
109. https://doi.org/10.1002/pan3.10256

31. Kulkarni R, Di Minin E. Automated retrieval of information on threatened species from online sources using machine learning. Methods Ecol Evol.

2021;12(7):1226–39. https://doi.org/10.1111/2041-210x.13608
32. Nundloll V, Smail R, Stevens C, Blair G. Automating the extraction of information from a historical text and building a linked data model for the
domain of ecology and conservation science. Heliyon. 2022;8(10):e10710. https://doi.org/10.1016/j.heliyon.2022.e10710 PMID: 36262290
33. Gerner M, Nenadic G, Bergman CM. LINNAEUS: a species name identification system for biomedical literature. BMC Bioinformatics. 2010;11:85.

https://doi.org/10.1186/1471-2105-11-85 PMID: 20149233

34. Pafilis E, Frankild SP, Fanini L, Faulwetter S, Pavloudi C, Vasileiadou A, et al. The SPECIES and ORGANISMS Resources for Fast and Accurate
Identification of Taxonomic Names in Text. PLoS One. 2013;8(6):e65390. https://doi.org/10.1371/journal.pone.0065390 PMID: 23823062
35. Ahmed S, Stoeckel M, Driller C, Pachzelt A, Mehler A. BIOfid Dataset: Publishing a German Gold Standard for Named Entity Recognition in His-
torical Biodiversity Literature. In: Proceedings of the 23rd Conference on Computational Natural Language Learning (CoNLL), Hong Kong, China:
Association for Computational Linguistics; 2019. https://doi.org/10.18653/v1/k19-1081
36. Nguyen NTH, Gabud RS, Ananiadou S. COPIOUS: A gold standard corpus of named entities towards extracting species occurrence from biodiver-
sity literature. Biodivers Data J. 2019;(7):e29626. https://doi.org/10.3897/BDJ.7.e29626 PMID: 30700967
37. Abdelmageed N, Löffler F, Feddoul L, Algergawy A, Samuel S, Gaikwad J, et al. BiodivNERE: Gold standard corpora for named entity recognition
and relation extraction in the biodiversity domain. Biodivers Data J. 2022;10:e89481. https://doi.org/10.3897/BDJ.10.e89481 PMID: 36761617
38. Le Guillarme N, Thuiller W. TaxoNERD: Deep neural models for the recognition of taxonomic entities in the ecological and evolutionary literature.

Methods Ecol Evol. 2021;13(3):625–41. https://doi.org/10.1111/2041-210x.13778
39. Varghese A, Allen K, Agyeman-Badu G, Haire J, Madsen R. Extraction of mitigation-related text from Endangered Species Act documents using
machine learning: a case study. Environ Syst Decis. 2021;42(1):63–74. https://doi.org/10.1007/s10669-021-09830-2

40. Nainia A, Vignes-Lebbe R, Chenin E, Sahraoui M, Mousannif H, Zahir J. A Transformer-Based Nlp Pipeline for Enhanced Extraction of Botanical
Information using Camembert on French Literature. In: Computer Science & Information Technology (CS & IT). vol. 14. Australia, NLP & Informa-
tion Retrieval, 2024. 59–78. https://doi.org/10.5121/csit.2024.140605
41. Wang J, Liu M, Zhao D, Shi S, Song W. Few-Shot NER in Marine Ecology Using Deep Learning. Communications in Computer and Information
Science. Springer Nature Singapore. 2023. p. 15–26. https://doi.org/10.1007/978-981-99-8145-8_2
42. Ehrmann M, Hamdi A, Pontes EL, Romanello M, Doucet A. Named Entity Recognition and Classification in Historical Documents: A Survey. ACM
Comput Surv. 2023;56(2):1–47. https://doi.org/10.1145/3604931
43. Tudor C, Megyesi B, Östling R. Prompting the Past: Exploring Zero-Shot Learning for Named Entity Recognition in Historical Texts Using
Prompt-Answering LLMs. In: In: Kazantseva A, Szpakowicz S, Degaetano-Ortlieb S, Bizzoni Y, Pagel J, editors. Proceedings of the 9th Joint
SIGHUM Workshop on Computational Linguistics for Cultural Heritage, Social Sciences, Humanities and Literature (LaTeCH-CLfL 2025), Albuquer-
que, New Mexico: Association for Computational Linguistics; 2025. 216–26. https://doi.org/10.18653/v1/2025.latechclfl-1.19
44. González-Gallardo CE, Tran HTH, Hamdi A, Doucet A. Leveraging Open Large Language Models for Historical Named Entity Recognition. In:
Antonacopoulos A, Hinze A, Piwowarski B, Coustaty M, Di Nunzio GM, Gelati F, et al., editors. Linking Theory and Practice of Digital Libraries.
Cham: Springer Nature Switzerland; 2024. p. 379–95. Available from: https://doi.org/10.1007/978-3-031-72437-4_22
45. Hiltmann T, Dröge M, Dresselhaus N, Grallert T, Althage M, Bayer P, et al. NER4all or Context is All You Need: Using LLMs for low-effort, high-
performance NER on historical texts. A humanities informed approach. In: arXiv, 2025. https://doi.org/10.48550/arXiv.2502.04351
46. Gougherty AV, Clipp HL. Testing the reliability of an AI-based large language model to extract ecological information from the scientific literature.

NPJ Biodivers. 2024;3(1):13. https://doi.org/10.1038/s44185-024-00043-9 PMID: 39242700
47. Scheepens D, Millard J, Farrell M, Newbold T. Large language models help facilitate the automated synthesis of information on potential pest con-
trollers. Methods Ecol Evol. 2024;15(7):1261–73. https://doi.org/10.1111/2041-210x.14341
48. Fillies JFM, Teich M, Karam N, Paschke A, Rehbein M. Historic to FAIR: Leveraging LLMs for Historic Term Identification and Standardization. In:

2025. 165–76. https://doi.org/10.18420/BTW2025-121

49. Holstein J, Monje JC. Taxon list of animals with German names (worldwide) compiled at the SMNS. Staatliche Naturwissenschaftliche Sammlun-
gen Bayerns. GBIF; 2016. https://doi.org/10.15468/6ouqwi

50. Farrell MJ, Le Guillarme N, Brierley L, Hunter B, Scheepens D, Willoughby A, et al. The changing landscape of text mining: a review of approaches
for ecology and evolution. Proc Biol Sci. 2024;291(2027):20240423. https://doi.org/10.1098/rspb.2024.0423 PMID: 39082244
51. Peeters R, Steiner A, Bizer C. Entity Matching using Large Language Models. 2024. http://arxiv.org/abs/2310.11244
52. Dorm F, Millard J, Purves D, Harfoot M, Mac Aodha O. Large language models possess some ecological knowledge, but how much?. Ecology.

2025. https://doi.org/10.1101/2025.02.10.637097

53. Elliott M, Fortes J. Using ChatGPT with Confidence for Biodiversity-Related Information Tasks. BISS. 2023;7. https://doi.org/10.3897/biss.7.112926
54. Castro A, Pinto J, Reino L, Pipek P, Capinha C. Large language models overcome the challenges of unstructured text data in ecology. Ecological
Informatics. 2024;82:102742. https://doi.org/10.1016/j.ecoinf.2024.102742
55. von Mittnacht F, Bach H. Königreich Würtemberg nebst Theilen der angrenzenden Länder: nach dem Massstab 1:200 000 in 4 Blättern als Gener-
alkarte des topographischen Atlasses. Stuttgart: K. statistisch-topographischen Bureau. 1848.

56. Redecker U, Schöntag W. Beiwort zu den Karten 7, 4-5: Verwaltungsgliederung in Baden, Württemberg und Hohenzollern 1815-1857. Verwal-
tungsgliederung in Baden, Württemberg und Hohenzollern 1858-1936. Historischer Atlas von Baden-Württemberg: Erläuterungen. Kommission für
geschichtliche Landeskunde in Baden-Württemberg. 1976. p. 1–24.