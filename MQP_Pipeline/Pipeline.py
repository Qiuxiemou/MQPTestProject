import argparse
import io
import re
import json
import os
import sys
import pandas as pd
import matplotlib.pyplot as plt
from tqdm import tqdm
from joblib import Parallel, delayed
import numpy as np
import json

from google.oauth2.service_account import Credentials
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload
import gspread

##### SAVED DATAFRAMES #####

DF = []
VIZ = []

##### AUTHENTICATION #####

SCOPES = [
    "https://www.googleapis.com/auth/drive",
    "https://www.googleapis.com/auth/spreadsheets"
]

URLIDS = [
    "1AoX5NgySpXNmzMIINfHsnlFV5UTIhkSM",  # Reaction Times
    "1du6Wcuouq6euEXlp5BbeLtRd6CwyKlaY",  # Playtest Logs
    "1u_GJhy9rYzqx_8HUe6ErhBH0GZevORLh9X8ohBr5rbc"  # Background Survey
]

creds = Credentials.from_service_account_file("credentials.json", scopes=SCOPES)
drive_service = build("drive", "v3", credentials=creds)
sheets_client = gspread.authorize(creds)

SAVE_DIR = "processed_participants"
os.makedirs(SAVE_DIR, exist_ok=True)

##### GOOGLE DRIVE HELPERS #####

def list_files(service, folder_id):
    results = service.files().list(
        q=f"'{folder_id}' in parents",
        fields="files(id, name, mimeType)"
    ).execute()
    return results.get("files", [])

def download_file(service, file_id):
    request = service.files().get_media(fileId=file_id)
    fh = io.BytesIO()
    downloader = MediaIoBaseDownload(fh, request)

    done = False
    while not done:
        _, done = downloader.next_chunk()

    fh.seek(0)
    return fh

def get_participant_folder(service, parent_folder_id, participant_id):
    folder_name = f"participant_{participant_id}"
    results = service.files().list(
        q=f"'{parent_folder_id}' in parents and name='{folder_name}' and mimeType='application/vnd.google-apps.folder'",
        fields="files(id, name)"
    ).execute()
    files = results.get("files", [])
    if not files:
        print(f"Warning: Folder for participant {participant_id} not found")
        return None
    return files[0]

##### REACTION TIMES #####

def load_reaction_times(service, folder_id):
    files = list_files(service, folder_id)
    reaction_data = {}

    for f in tqdm(files, desc="Loading reaction times"):
        match = re.search(r"Tester_(\d+)", f["name"])
        if not match:
            continue

        pid = int(match.group(1))
        content = download_file(service, f["id"]).read().decode("utf-8")
        values = [float(x) for x in content.strip().split(",")]

        if len(values) != 11:
            print(f"Warning: Unexpected format in {f['name']}")
            continue

        cleaned = clean_reaction_times(values[:10])

        reaction_data[pid] = {
            "reaction_times": cleaned,
            "too_fast_count": values[10],
            "avg_reaction": sum(cleaned) / len(cleaned)
        }

    return reaction_data

def clean_reaction_times(values):
    svals = sorted(values)
    q1 = svals[2]
    q3 = svals[7]
    iqr = 1.5 * (q3 - q1)
    for n in range(9, -1, -1):
        if (svals[n] > q3 + iqr) or (svals[n] < q1 - iqr):
            svals.pop(n)
    return svals

##### PARTICIPANT DATA #####

def load_participant(service, participant_folder):
    pdata = {"rounds": {}}

    files = list_files(service, participant_folder["id"])

    # ---- LOAD SUMMARY FILE ----
    summary_file = None
    for f in files:
        if f["name"].startswith("summary") and f["name"].endswith(".csv"):
            summary_file = f
            break

    if summary_file:
        content = download_file(service, summary_file["id"])
        pdata["summary"] = pd.read_csv(content)
    else:
        pdata["summary"] = None

    # ---- LOAD ROUNDS ----
    round_folders = [f for f in files if f["name"].startswith("round_")]

    for f in tqdm(round_folders, desc=f"Loading rounds for {participant_folder['name']}"):
        round_id = int(f["name"].split("_")[1])
        round_files = list_files(service, f["id"])

        rdata = {}
        for rf in round_files:
            rname = rf["name"]
            content = download_file(service, rf["id"])

            if rname == "world.csv":
                rdata["world"] = pd.read_csv(content)
            elif rname == "ShotEvent.csv":
                rdata["shots"] = pd.read_csv(content)
            elif rname == "PlayerInput.csv":
                rdata["inputs"] = pd.read_csv(content)
            elif rname == "config.json":
                rdata["config"] = json.load(content)

        pdata["rounds"][round_id] = rdata

    return pdata

def save_participant(pid, pdata, save_dir=SAVE_DIR):
    filepath = os.path.join(save_dir, f"participant_{pid}.pkl")
    with open(filepath, "wb") as f:
        import pickle
        pickle.dump(pdata, f)

def load_saved_participants(save_dir=SAVE_DIR):
    dataset = {}
    if not os.path.exists(save_dir):
        return dataset
    for fname in os.listdir(save_dir):
        if fname.endswith(".pkl"):
            pid = int(fname.split("_")[1].split(".")[0])
            with open(os.path.join(save_dir, fname), "rb") as f:
                import pickle
                dataset[pid] = pickle.load(f)
    return dataset

##### SURVEY DATA #####

def load_survey(client, sheet_id):
    sheet = client.open_by_key(sheet_id).sheet1
    data = sheet.get_all_records()
    return pd.DataFrame(data)

##### PARTICIPANT PROCESSING #####

def process_one_participant(pid, reaction_times, survey_df, playtest_folder_id):
    folder = get_participant_folder(drive_service, playtest_folder_id, pid)
    if folder is None:
        return None

    pdata = load_participant(drive_service, folder)
    pdata["reaction"] = reaction_times.get(pid)
    pdata["survey"] = survey_df[survey_df["participant_id"] == pid]

    save_participant(pid, pdata)
    return pid

def process_participants(pid_start, pid_end, reaction_times, survey_df, playtest_folder_id, n_jobs=20):
    participant_ids = list(range(pid_start, pid_end + 1))

    # Run in parallel
    Parallel(n_jobs=n_jobs)(
        delayed(process_one_participant)(pid, reaction_times, survey_df, playtest_folder_id)
        for pid in participant_ids
    )

##### DF HELPER FUNCTIONS #####

def get_df(df_name):
    for df, name in DF:
        print(name, df_name)
        if name == df_name:
            return df
    return None

def register_DFs(dataset):
    DF.append(example_df(dataset))

##### VISUALIZATIONS #####

def scatter_plot(df_name, x_field, y_field, x_label, y_label, title):
    df = get_df(df_name)
    plt.scatter(df[x_field], df[y_field])
    plt.xlabel(x_label)
    plt.ylabel(y_label)
    plt.title(title)
    plt.show()

def histogram(df_name, x_field, x_label, title):
    df = get_df(df_name)
    plt.hist(df[x_field], bins=20)
    plt.xlabel(x_label)
    plt.ylabel("Frequency")
    plt.title(title)
    plt.show()

##### DEBUGGING SUPPORT #####

def export_dataset_structure(dataset, output_file="dataset_structure.json"):
    structure = {}

    for pid, pdata in dataset.items():
        structure[pid] = {
            "rounds": list(pdata["rounds"].keys()),
            "has_reaction": pdata.get("reaction") is not None,
            "has_survey": pdata.get("survey") is not None,
            "has_summary": pdata.get("summary") is not None,
        }

        # optional deeper inspection
        structure[pid]["round_details"] = {
            rid: list(rdata.keys())
            for rid, rdata in pdata["rounds"].items()
        }

    with open(output_file, "w") as f:
        json.dump(structure, f, indent=4, default=str)

##### DATAFRAME BUILDER [WHERE YOU CAN CREATE VARIOUS DATAFRAMES OF INTEREST #####
    # Make sure to always returns a tuple that contains the dataframe and a string for its name:
    # Example -> return tuple([pd.DataFrame(rows), 'example'])

def example_df(dataset):
    rows = []

    for pid, pdata in dataset.items():
        reaction = pdata.get("reaction")

        for rid, rdata in pdata["rounds"].items():
            shotdf = rdata.get("shots")

            if shotdf is not None and not shotdf.empty:
                filtered_shots = shotdf[shotdf["weapon"] != 1]
                shots = len(filtered_shots)

                rows.append({
                    "participant": pid,
                    "round": rid,
                    "shots": shots,
                    "avg_reaction": reaction["avg_reaction"] if reaction else np.nan,
                    "too_fast": reaction["too_fast_count"] if reaction else np.nan
                })

    return tuple([pd.DataFrame(rows), 'example'])







##### MAIN EXECUTION #####
    # TO IMPORT DATA: python Pipeline.py -in X -ix Y
        # Need either -in, -ix, or both to import (assumed no data is being imported otherwise)
        # X is the lower range of the participants you want to import
        # Y is the upper range of the participants you want to import
    # TO VISUALIZE DATA: python Pipeline.py -df A -v B -xf C -yf D -xl E -yl F -t G
        # Need to have -df, -v, -xf, and -t for all (some graphs require others)
        # A is the name of the dataframe of interest
        # B is the type of graph you would like to make
        # C is the attribute in the dataframe you would like to make the x field
        # D is the attribute in the dataframe you would like to make the y field
        # E is the x label you would like for the graph
        # F is the y label you would like for the graph
        # G is the title you would like for the graph

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument('-in', '--min_pid', type=int, help="Min participant ID")
    parser.add_argument('-ix', '--max_pid', type=int, help="Max participant ID")
    parser.add_argument('-df', '--dataframe', type=str, help="name of the dataframe being loaded")
    parser.add_argument('-v', '--viz', type=str, help="Visualization type")
    parser.add_argument('-xf', '--x_field', type=str, help="What field should be used as the x-axis")
    parser.add_argument('-yf', '--y_field', type=str, help="What field should be used as the y-axis")
    parser.add_argument('-xl', '--x_label', type=str, help="What label should be used as the x-axis")
    parser.add_argument('-yl', '--y_label', type=str, help="What label should be used as the y-axis")
    parser.add_argument('-t', '--title', type=str, help="What should the graph be titled")
    args = parser.parse_args()

    if (args.max_pid != None) or (args.min_pid != None):
        PID_START = args.min_pid if args.min_pid != None else 3
        PID_END = args.max_pid if (args.max_pid != None) & (args.max_pid > 3) else 30

        REACTION_FOLDER_ID = URLIDS[0]
        PLAYTEST_FOLDER_ID = URLIDS[1]
        SHEET_ID = URLIDS[2]

        # Load data
        reaction_times = load_reaction_times(drive_service, REACTION_FOLDER_ID)
        survey_df = load_survey(sheets_client, SHEET_ID)

        # Process participants
        process_participants(
            PID_START,
            PID_END,
            reaction_times,
            survey_df,
            PLAYTEST_FOLDER_ID,
            n_jobs=20
        )

    # Load saved participants and build dataframes
    dataset = load_saved_participants()
    export_dataset_structure(dataset)
    register_DFs(dataset)

    # Visualize if given dataframe should be a scatter plot
    if (args.viz == "Scatter") & (args.dataframe != None) & (args.x_field != None) & (args.y_field != None) & (args.title != None): scatter_plot(args.dataframe, args.x_field, args.y_field, args.x_lable, args.y_label, args.title) if (args.x_label != None) & (args.y_label != None) else scatter_plot(args.dataframe, args.x_field, args.y_field, args.x_field, args.y_field, args.title)

    # Visualize if given dataframe should be a histogram
    if (args.viz == "Histogram") & (args.dataframe != None) & (args.x_field != None) & (args.title != None): histogram(args.dataframe, args.x_field, args.x_lable, args.title) if (args.x_label != None) else scatter_plot(args.dataframe, args.x_field, args.x_field, args.title)