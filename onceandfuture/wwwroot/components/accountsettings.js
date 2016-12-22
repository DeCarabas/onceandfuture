import React from 'react';
import { connect } from 'react-redux';
import {
  COLOR_VERY_LIGHT,

  SIZE_BANNER_HEIGHT,
  SIZE_SPACER_HEIGHT,

  Z_INDEX_ACCOUNT_SETTINGS,
} from './style';
import {
  accountSettingsToggle,
  setEmail,
} from '../actions';
import {
  SettingInputBox,
  SettingPasswordBox,
  SettingsSectionTitle,
} from './settingscontrols';
import IconButton from './iconbutton';

const HeaderBox = ({onClose}) => {
  const style = {
    position: "relative",
    height: SIZE_BANNER_HEIGHT,
  };

  const h_style = {
    position: "absolute",
    top: 5,
    margin: 0,
  };

  const b_style = {
    position: "absolute",
    top: 0, right: 0,
  };

  return <div style={style}>
    <h2 style={h_style}>Account Settings</h2>
    <div style={b_style}>
      <IconButton tip="Close" icon="fa-window-close" onClick={onClose} />
    </div>
  </div>;
}

const ChangeEmailBox = ({email, emailState, setAddress}) => {
  let middlePart;
  if (emailState === 'PENDING') {
    const style = {
      textAlign: 'center',
      padding: SIZE_SPACER_HEIGHT,
    };
    middlePart = <div style={style}>(Fetching, please wait.)</div>;
  } else if (emailState === 'ERROR') {
    const style = {
      textAlign: 'center',
      padding: SIZE_SPACER_HEIGHT,
    };
    middlePart = <div style={style}>An unexpected error occurred. Please try again!</div>;
  } else {
    console.log('YO BUDDY', email, emailState);
    let validator = (value) => {
      // No message but not enabled.
      console.log(email, emailState, value);
      if (value === email) { return ''; }
      if (emailState === 'SETTING') { return ''; }

      if (!value || value.length == 0) {
        return 'Cannot have an empty address.';
      }

      return null;
    };

    middlePart = <div>
      <p>Change your email address! Right here!</p>
      <SettingInputBox
        value={email}
        setValue={setAddress}
        buttonLabel='Change Email'
        validator={validator}
      />
    </div>;
  }

  return (
    <div>
      <SettingsSectionTitle text="Change Email Address" />
      {middlePart}
    </div>
  );
};

const ChangePasswordBox = ({setPassword}) => {
  return (
    <div>
      <SettingsSectionTitle text="Change Password" />
      <p>Change your email password.</p>
      <SettingPasswordBox setValue={setPassword} buttonLabel='Change' />
    </div>
  );
};

const AccountSettingsBase = ({user, email, emailState, onClose, onSetAddress}) => {
  const style = {
    width: 460,
    marginLeft: 'auto',
    marginRight: 'auto',
    backgroundColor: COLOR_VERY_LIGHT,
    padding: 10,
    paddingBottom: 20,
    border: "2px solid black",
    borderRadius: 10,

    zIndex: Z_INDEX_ACCOUNT_SETTINGS,
  };

  const setAddress = (email) => onSetAddress(user, email);
  return <div style={style}>
    <HeaderBox onClose={onClose} />
    <ChangeEmailBox email={email} emailState={emailState} setAddress={setAddress} />
    <ChangePasswordBox />
  </div>;
};


const mapStateToProps = (state) => {
  return {
    emailState: state.account_settings.emailState,
    email: state.account_settings.email,
    user: state.user,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onClose: () => dispatch(accountSettingsToggle()),
    onSetAddress: (user, email) => dispatch(setEmail(user, email)),
  };
};

const AccountSettings = connect(mapStateToProps, mapDispatchToProps)(AccountSettingsBase);

export default AccountSettings;
