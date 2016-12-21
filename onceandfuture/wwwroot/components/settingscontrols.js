import React from 'react';
import {
  COLOR_DARK,
  COLOR_DARK_GREY,
  COLOR_LIGHT_GREY,
  COLOR_VERY_DARK,
  COLOR_VERY_LIGHT_GREY,

  SIZE_SPACER_HEIGHT,
} from './style';


export const SettingsSectionTitle = ({text}) => {
  const style = {
    fontWeight: 'bold',
  };

  return <div style={style}>{text}</div>;
}

export const SettingsButton = ({onClick, text, enabled=true}) => {
  const div_style = {
    textAlign: 'right',
    marginTop: SIZE_SPACER_HEIGHT,
  };
  const base_style = {
    padding: 3,
  };

  let style, handler;
  if (enabled) {
    style = Object.assign({}, base_style, {
      cursor: 'pointer',
      color: 'white',
      backgroundColor: COLOR_DARK,
      border: '2px solid ' + COLOR_VERY_DARK,
    });
    handler = onClick;
  } else {
    style = Object.assign({}, base_style, {
      cursor: 'default',
      color: COLOR_VERY_LIGHT_GREY,
      backgroundColor: COLOR_LIGHT_GREY,
      border: '2px solid ' + COLOR_LIGHT_GREY,
    });
    handler = null;
  }

  return (
    <div style={div_style} >
      <span style={style} onClick={handler}>{text}</span>
    </div>
  );
};

export class SettingInputBox extends React.Component {
  constructor(props) {
    super(props);
    this.state = {value: props.value || ''};

    this.buttonLabel = props.buttonLabel;
    this.kind = props.kind || "text";
    this.setValue = props.setValue;

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }

  handleChange(event) {
    this.setState({value: event.target.value});
  }

  handleSubmit(event) {
    this.setValue(this.state.value);
    event.preventDefault();
  }

  render() {
    const input_style = {
      width: '100%',
    };

    return <div>
      <input style={input_style} type={this.kind} value={this.state.value} onChange={this.handleChange} />
      <SettingsButton onClick={this.handleSubmit} text={this.buttonLabel} />
   </div>;
  }
}

export class SettingPasswordBox extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      value_first: '',
      value_second: '',
    };
    this.setValue = props.setValue;

    this.isValid = this.isValid.bind(this);
    this.handleChangeFirst = this.handleChangeFirst.bind(this);
    this.handleChangeSecond = this.handleChangeSecond.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }

  handleChangeFirst(event) {
    const new_state = Object.assign({}, this.state, {
      value_first: event.target.value,
    });
    this.setState(new_state);
  }

  handleChangeSecond(event) {
    const new_state = Object.assign({}, this.state, {
      value_second: event.target.value,
    });
    this.setState(new_state);
  }

  handleSubmit(event) {
    if (this.isValid()) {
      this.setValue(this.state.value);
      event.preventDefault();
    }
  }

  invalidReason() {
    if (this.state.value_first == '') {
      return '';
    } else if (this.state.value_first !== this.state.value_second) {
      return "The two passwords don't match.";
    } else {
      return null;
    }
  }

  isValid() {
    return this.invalidReason() === null;
  }

  render() {
    const input_style = {
      width: '100%',
    };

    const input_second = Object.assign({}, input_style, {
      marginTop: SIZE_SPACER_HEIGHT,
    });

    const button_pusher_style = {
      float: 'right',
    };

    const validation_error_style = Object.assign({}, button_pusher_style, {
      color: 'red',
      paddingTop: 10,
      paddingRight: 5,
    });

    return <div>
      <input
        style={input_style}
        type="password"
        placeholder="New Password Here!"
        value={this.state.value_first}
        onChange={this.handleChangeFirst}
      />
      <input
        style={input_second}
        type="password"
        placeholder="New Password Again, Please!"
        value={this.state.value_second}
        onChange={this.handleChangeSecond}
      />
      <div>
        <div style={button_pusher_style}>
          <SettingsButton
            onClick={this.handleSubmit}
            text={this.props.buttonLabel}
            enabled={this.isValid()}
          />
        </div>
        <div style={validation_error_style}>
          <span>{this.invalidReason()}</span>
        </div>
        &nbsp;
        <div style={{float: 'clear'}} />
      </div>
   </div>;
  }
}
